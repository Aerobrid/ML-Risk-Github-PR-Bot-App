from fastapi import APIRouter, UploadFile, File, HTTPException
from app.schemas import RiskRequest, RiskResponse, ModelInfo, SecurityScanRequest, SecurityScanResponse
from typing import Optional
from app.core.model_manager import ModelManager
from app.core.scanner import get_scanner
import os
import logging

router = APIRouter()
logger = logging.getLogger(__name__)
model_manager = ModelManager.get_instance()
scanner = get_scanner()
DISABLED_MSG = (
    "Model management via API is disabled in this deployment. "
    "Train and manage models via the CLI: cd ml-service && python train_xgboost_model.py"
)

def calculate_rule_based_score(request: RiskRequest) -> tuple[float, dict]:
    score = 0.0
    details = {}

    commit_score = min(request.commitCount / 50.0, 1.0) * 0.25
    score += commit_score
    details["commitImpact"] = round(commit_score, 3)

    lines_score = min(request.linesChanged / 2000.0, 1.0) * 0.30
    score += lines_score
    details["sizeImpact"] = round(lines_score, 3)

    test_score = (1.0 - request.testPassRate) * 0.25
    score += test_score
    details["testImpact"] = round(test_score, 3)

    time_risk = 0.0
    if request.dayOfWeek in [5, 6]:
        time_risk += 0.10
    if request.hourOfDay < 8 or request.hourOfDay > 18:
        time_risk += 0.10

    score += time_risk
    details["timeImpact"] = round(time_risk, 3)

    score = min(score, 1.0)
    details["finalScore"] = round(score, 3)
    return score, details

@router.post("/predict", response_model=RiskResponse)
def predict(request: RiskRequest):
    details = {}
    score = 0.0
    
    # 1. Run ML Prediction (XGBoost)
    if model_manager.risk_model:
        try:
            features = [
                request.commitCount,
                request.linesChanged,
                request.testPassRate,
                request.hourOfDay,
                request.dayOfWeek
            ]
            score = model_manager.predict_risk(features)
            details["source"] = "XGBoost Risk Model"
        except Exception as e:
            logger.error(f"ML prediction failed: {e}")
            score, details = calculate_rule_based_score(request)
            details["source"] = "Rule-based (Fallback)"
    else:
        score, details = calculate_rule_based_score(request)
        details["source"] = "Rule-based (Default)"

    # 2. Run Analysis (Heuristics + Swappable Model if loaded)
    scan_report = scanner.scan(request.files)
    
    # If a swappable analysis model is loaded, we could use it here
    if model_manager.analysis_model:
        # For now, we just note that it's active
        details["analysis_model"] = model_manager.analysis_model_name or "active"

    if any(v.severity == "CRITICAL" for v in scan_report):
        details["securityPenalty"] = 0.2
        score = min(score + 0.2, 1.0)

    # Determine risk level
    if score < 0.3: level = "LOW"
    elif score < 0.5: level = "MEDIUM"
    elif score < 0.8: level = "HIGH"
    else: level = "CRITICAL"

    return RiskResponse(
        riskScore=round(score, 3),
        riskLevel=level,
        details=details,
        scanReport=scan_report
    )

@router.get("/models", response_model=list[ModelInfo])
def list_models():
    # Endpoint commented out to prevent API-based model management.
    # The original implementation is preserved below for reference.
    #
    # return model_manager.list_models()
    #
    # raise HTTPException(status_code=403, detail=DISABLED_MSG)
    pass

@router.post("/models/upload")
async def upload_model(file: UploadFile = File(...), name: Optional[str] = None):
    # Endpoint commented out to prevent API-based model uploads.
    # Original implementation (preserved for reference):
    # try:
    #     filename = f"{name}_{file.filename}" if name else file.filename
    #     os.makedirs(model_manager.models_dir, exist_ok=True)
    #     file_path = os.path.join(model_manager.models_dir, filename)
    #     contents = await file.read()
    #     with open(file_path, "wb") as buffer:
    #         buffer.write(contents)
    #     if filename == "dummy_risk_model.pkl":
    #         model_manager.load_risk_model(filename)
    #     else:
    #         model_manager.load_analysis_model(filename)
    #     return {"message": f"Model uploaded: {filename}", "filename": filename}
    # except Exception as e:
    #     logger.exception("Failed to upload model")
    #     raise HTTPException(status_code=500, detail=str(e))
    pass


@router.delete("/models/{filename}")
def delete_model(filename: str):
    # Endpoint commented out to prevent API-based model deletion.
    # Original implementation (preserved for reference):
    # if filename == "dummy_risk_model.pkl":
    #     raise HTTPException(status_code=403, detail="Cannot delete built-in risk model")
    # path = os.path.join(model_manager.models_dir, filename)
    # if not os.path.exists(path):
    #     raise HTTPException(status_code=404, detail="Model not found")
    # try:
    #     os.remove(path)
    #     if getattr(model_manager, "analysis_model_name", None) == filename:
    #         model_manager.analysis_model = None
    #         model_manager.analysis_model_name = None
    #     return {"message": f"Deleted {filename}"}
    # except Exception as e:
    #     logger.exception("Failed to delete model")
    #     raise HTTPException(status_code=500, detail=str(e))
    pass

@router.post("/models/{filename}/load")
def load_model(filename: str):
    # Endpoint commented out to prevent API-based model loading.
    # Original implementation (preserved for reference):
    # if filename == "dummy_risk_model.pkl":
    #     if model_manager.load_risk_model(filename):
    #         return {"message": "Risk model reloaded"}
    #     else:
    #         raise HTTPException(status_code=404, detail="Risk model not found")
    # if model_manager.load_analysis_model(filename):
    #     return {"message": f"Analysis model {filename} loaded and active"}
    # else:
    #     raise HTTPException(status_code=404, detail="Model not found or failed to load")
    pass

@router.post("/security/scan", response_model=SecurityScanResponse)
def security_scan(request: SecurityScanRequest):
    # Backward compatibility or standalone scan endpoint
    # We map this to our internal scanner too
    
    # Convert SecurityScanRequest to list of FileChange (mocking)
    files = []
    for i, content in enumerate(request.content_snippets):
        filename = request.files[i] if i < len(request.files) else f"file_{i}"
        files.append(type('obj', (object,), {'filename': filename, 'patch': content}))
    
    vulnerabilities = scanner.scan(files)
    return {"status": "completed", "vulnerabilities": vulnerabilities}

@router.get("/health")
def health():
    return {"status": "healthy", "version": "2.1 (XGBoost)"}
