# ML Model Training Guide

This guide explains how to train new risk assessment models for the Deployment Risk Platform.

## 1. Environment Setup

Ensure you have Python 3.10+ installed.

```bash
cd ml-service
# Create venv
python -m venv venv
# Activate venv (Windows)
.\venv\Scripts\activate
# Activate venv (Linux/Mac)
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt
# Note: For training, you also need scikit-learn
pip install scikit-learn
```

## 2. Training with Real Data 

To make the model accurate to your specific project, you should train it on your repository's history.

1.  **Set your GitHub Token:**
    ```bash
    # Windows PowerShell
    $env:GITHUB_TOKEN="your_personal_access_token"
    # Linux/Mac
    export GITHUB_TOKEN="your_personal_access_token"
    ```

2.  **Fetch Historical Data:**
    Run the miner script with your repo name (e.g., `owner/repo`).
    ```bash
    python fetch_historical_data.py my-org/my-repo
    ```
    This will generate `data/historical_pr_data.csv` containing metrics from your last ~500 merged PRs, with "Risk Scores" calculated based on whether they were hotfixes or complex merges.

3.  **Train the Model:**
    ```bash
    python train_xgboost_model.py
    ```
    The script will automatically detect `data/historical_pr_data.csv` and use it instead of synthetic data.

## 3. Training with Synthetic Data (Jumpstart)

If you don't have historical data yet, simply run the training script without the CSV file:

```bash
python train_xgboost_model.py
```
It will generate 10,000 synthetic samples to initialize the model with basic heuristics.

## 4. Deploying/Swapping Models

The ML Service supports hot-swapping models via API.

### Upload a new model
```bash
curl -X POST -F "file=@models/my_rf_model.pkl" http://localhost:8000/models/upload
```

### Load a model
```bash
curl -X POST "http://localhost:8000/models/my_rf_model.pkl/load"
```