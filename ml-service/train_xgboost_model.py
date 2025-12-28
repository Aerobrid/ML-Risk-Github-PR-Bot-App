"""
XGBoost Risk Scoring Model Training Script

This script trains the main risk scoring model used by the ML service.

USAGE:
  python train_xgboost_model.py

TRAINING STRATEGY:
  1. Cold Start: No historical data → Generate 10,000 synthetic samples
  2. Warming Up: <100 real samples → Blend real + synthetic data
  3. Mature: ≥100 real samples → Use only real data

OUTPUT:
  - models/xgboost_risk_model_v2.json (XGBoost native format)
  - models/dummy_risk_model.pkl (pickle format for compatibility)

RETRAINING:
  Run this script monthly after collecting real PR data:
    make collect-data REPO=your-org/repo LIMIT=1000
    make train
    docker-compose restart ml-service
"""
import xgboost as xgb
import pandas as pd
import numpy as np
import os
from sklearn.model_selection import train_test_split
from sklearn.metrics import mean_squared_error, accuracy_score, classification_report
import logging

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

def generate_synthetic_data(n_samples=10000):
    """
    Generates enhanced synthetic data for training the risk model.
    Features:
    - commit_count: Number of commits in the PR
    - lines_changed: Total lines added + deleted
    - test_pass_rate: Percentage of tests passed (0.0 to 1.0)
    - hour_of_day: Hour the PR was opened/updated (0-23)
    - day_of_week: Day of the week (0=Monday, 6=Sunday)

    Target:
    - risk_score: 0.0 (Low) to 1.0 (Critical)

    Enhanced with more realistic patterns and stronger correlations.
    """
    np.random.seed(42)

    # Generate more realistic distributions
    data = {
        'commit_count': np.random.poisson(lam=5, size=n_samples),
        'lines_changed': np.random.exponential(scale=200, size=n_samples),
        'test_pass_rate': np.random.beta(a=8, b=1, size=n_samples),  # Skewed towards 1.0
        'hour_of_day': np.random.randint(0, 24, size=n_samples),
        'day_of_week': np.random.randint(0, 7, size=n_samples)
    }

    df = pd.DataFrame(data)

    # Add variation: some PRs with very large changes (outliers)
    outlier_mask = np.random.random(n_samples) < 0.05  # 5% outliers
    df.loc[outlier_mask, 'lines_changed'] *= np.random.uniform(5, 15, outlier_mask.sum())
    df.loc[outlier_mask, 'commit_count'] += np.random.randint(10, 30, outlier_mask.sum())

    # Introduce stronger correlations for Risk Score
    # Start with base risk
    risk = np.random.normal(0.15, 0.08, n_samples)

    # 1. Large changesets = higher risk (exponential relationship)
    lines_risk = np.where(
        df['lines_changed'] > 1000, 0.35,
        np.where(df['lines_changed'] > 500, 0.20,
        np.where(df['lines_changed'] > 200, 0.10, 0.0))
    )
    risk += lines_risk

    # 2. Many commits = instability/churn
    commit_risk = np.where(
        df['commit_count'] > 20, 0.25,
        np.where(df['commit_count'] > 10, 0.15,
        np.where(df['commit_count'] > 5, 0.05, 0.0))
    )
    risk += commit_risk

    # 3. Low test pass rate = critical risk factor
    test_risk = (1.0 - df['test_pass_rate']) * 0.6
    risk += test_risk

    # 4. Weekend deployments (Saturday=5, Sunday=6)
    weekend_risk = df['day_of_week'].isin([5, 6]).astype(float) * 0.20
    risk += weekend_risk

    # 5. Friday deployments (slightly risky)
    friday_risk = (df['day_of_week'] == 4).astype(float) * 0.10
    risk += friday_risk

    # 6. After-hours commits (before 8am or after 6pm)
    after_hours = ((df['hour_of_day'] < 8) | (df['hour_of_day'] > 18)).astype(float) * 0.15
    risk += after_hours

    # 7. Combo: Large change + weekend = extra risky
    combo_risk = ((df['lines_changed'] > 500) & (df['day_of_week'].isin([5, 6]))).astype(float) * 0.15
    risk += combo_risk

    # 8. Low risk scenarios: small changes during business hours
    low_risk = ((df['lines_changed'] < 100) &
                (df['commit_count'] <= 3) &
                (df['test_pass_rate'] > 0.9) &
                (df['hour_of_day'] >= 9) &
                (df['hour_of_day'] <= 17) &
                (df['day_of_week'] < 5)).astype(float)
    risk -= low_risk * 0.10

    # Add some noise for realism
    risk += np.random.normal(0, 0.05, n_samples)

    # Clip to valid range
    risk = np.clip(risk, 0.0, 1.0)

    df['risk_score'] = risk

    # Log distribution
    logger.info(f"Generated {n_samples} synthetic samples")
    logger.info(f"  LOW risk (<0.3): {(risk < 0.3).sum()} ({(risk < 0.3).sum()/n_samples*100:.1f}%)")
    logger.info(f"  MEDIUM risk (0.3-0.5): {((risk >= 0.3) & (risk < 0.5)).sum()} ({((risk >= 0.3) & (risk < 0.5)).sum()/n_samples*100:.1f}%)")
    logger.info(f"  HIGH risk (0.5-0.8): {((risk >= 0.5) & (risk < 0.8)).sum()} ({((risk >= 0.5) & (risk < 0.8)).sum()/n_samples*100:.1f}%)")
    logger.info(f"  CRITICAL risk (>0.8): {(risk >= 0.8).sum()} ({(risk >= 0.8).sum()/n_samples*100:.1f}%)")

    return df

def train_model():
    """
    Train the XGBoost risk scoring model.

    Training strategy:
    - If no historical data exists: Use 10,000 synthetic samples (cold start)
    - If <100 real samples: Augment with 5,000 synthetic samples
    - If ≥100 real samples: Use only real data

    The trained model is saved to:
    - models/xgboost_risk_model_v2.json (XGBoost format)
    - models/dummy_risk_model.pkl (pickle format - loaded by ML service)
    """
    data_path = "data/historical_pr_data.csv"

    # Cold Start Strategy: Check for historical data
    if os.path.exists(data_path):
        logger.info(f"Loading REAL historical data from {data_path}...")
        df = pd.read_csv(data_path)
        # Ensure columns exist
        required_cols = ['commit_count', 'lines_changed', 'test_pass_rate', 'hour_of_day', 'day_of_week', 'risk_score']
        if not all(col in df.columns for col in required_cols):
             logger.warning("Historical data missing columns. Falling back to synthetic.")
             df = generate_synthetic_data(10000)
        elif len(df) < 100:
             # Cold start augmentation: too few real samples, augment with synthetic
             logger.warning(f"Only {len(df)} real samples found. Augmenting with synthetic data...")
             synthetic_df = generate_synthetic_data(5000)
             df = pd.concat([df, synthetic_df], ignore_index=True)
             logger.info(f"Training set augmented to {len(df)} samples (real + synthetic)")
        else:
             logger.info(f"Using {len(df)} real samples for training")
    else:
        logger.info("No historical data found. Cold start mode: Generating SYNTHETIC training data...")
        df = generate_synthetic_data(10000)
    
    X = df[['commit_count', 'lines_changed', 'test_pass_rate', 'hour_of_day', 'day_of_week']]
    y = df['risk_score']
    
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
    
    logger.info(f"Training XGBoost Regressor on {len(X_train)} samples...")
    
    model = xgb.XGBRegressor(
        objective='reg:squarederror',
        n_estimators=200,
        learning_rate=0.05,
        max_depth=6,
        subsample=0.8,
        colsample_bytree=0.8,
        random_state=42
    )
    
    model.fit(X_train, y_train)
    
    # Evaluate model performance
    preds = model.predict(X_test)
    mse = mean_squared_error(y_test, preds)
    logger.info(f"Model MSE: {mse:.4f}")

    # Save model in multiple formats
    # 1. XGBoost native format (recommended)
    model_path = "models/xgboost_risk_model_v2.json"
    logger.info(f"Saving model to {model_path}...")
    # Ensure models directory exists
    os.makedirs(os.path.dirname(model_path) or "models", exist_ok=True)

    # Use the underlying Booster to save the native XGBoost model to avoid
    # sklearn wrapper issues with _estimator_type in some xgboost versions.
    booster = model.get_booster()
    booster.save_model(model_path)

    # 2. Pickle format for backward compatibility
    # This is the file loaded by the ML service on startup
    import pickle
    with open("models/dummy_risk_model.pkl", "wb") as f:
        pickle.dump(model, f)

    logger.info("✅ Training complete!")
    logger.info("📝 To use the new model:")
    logger.info("   1. Restart ML service: docker-compose restart ml-service")
    logger.info("   2. Or hot-reload: curl -X POST http://localhost:8000/models/dummy_risk_model.pkl/load")

if __name__ == "__main__":
    train_model()
