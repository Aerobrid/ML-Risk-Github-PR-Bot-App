import pickle
import os
from typing import Optional, Any, Dict
import logging

logger = logging.getLogger(__name__)

class ModelManager:
    _instance = None

    def __init__(self):
        self.risk_model: Optional[Any] = None
        self.analysis_model: Optional[Any] = None
        self.analysis_model_name: Optional[str] = None
        
        self.models_dir = "ml-service/models" if os.path.exists("ml-service/models") else "models"
        os.makedirs(self.models_dir, exist_ok=True)
        
        # Load default risk model if exists
        self.load_risk_model("dummy_risk_model.pkl")

    @classmethod
    def get_instance(cls):
        if cls._instance is None:
            cls._instance = cls()
        return cls._instance

    def load_risk_model(self, model_name: str) -> bool:
        path = os.path.join(self.models_dir, model_name)
        if not os.path.exists(path):
            return False
        try:
            with open(path, "rb") as f:
                self.risk_model = pickle.load(f)
            logger.info(f"Loaded risk model: {model_name}")
            return True
        except Exception as e:
            logger.error(f"Failed to load risk model: {e}")
            return False

    def load_analysis_model(self, model_name: str) -> bool:
        """Loads a model for security/bug analysis."""
        path = os.path.join(self.models_dir, model_name)
        if not os.path.exists(path):
            return False
        try:
            with open(path, "rb") as f:
                self.analysis_model = pickle.load(f)
            self.analysis_model_name = model_name
            logger.info(f"Loaded analysis model: {model_name}")
            return True
        except Exception as e:
            logger.error(f"Failed to load analysis model: {e}")
            return False

    def predict_risk(self, features: list) -> float:
        if not self.risk_model:
            raise ValueError("Risk model not loaded")
        return self._predict(self.risk_model, features)

    def _predict(self, model: Any, features: list) -> float:
        try:
            if hasattr(model, "predict_proba"):
                return float(model.predict_proba([features])[0][1])
            elif hasattr(model, "predict"):
                res = model.predict([features])
                return float(res[0])
            else:
                raise ValueError("Model format not supported")
        except Exception as e:
            logger.error(f"Prediction error: {e}")
            raise

    def list_models(self) -> list:
        models = []
        for f in os.listdir(self.models_dir):
            if f.endswith(('.pkl', '.joblib', '.onnx', '.pth')):
                path = os.path.join(self.models_dir, f)
                is_risk = f == "dummy_risk_model.pkl"
                models.append({
                    "name": f,
                    "type": os.path.splitext(f)[1],
                    "size_bytes": os.path.getsize(path),
                    "active": f == self.analysis_model_name or is_risk,
                    "purpose": "Risk Scoring (Fixed)" if is_risk else "Code Analysis (Swappable)"
                })
        return models