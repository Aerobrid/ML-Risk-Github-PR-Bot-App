from fastapi import FastAPI, UploadFile, File, HTTPException
from pydantic import BaseModel, Field
from typing import Dict, List, Optional, Tuple
import os
import shutil

app = FastAPI(title="ML Risk Service")

# models dir for trained models
MODELS_DIR = "models"
os.makedirs(MODELS_DIR, exist_ok=True)

# Request model matches ASP.NET RiskRequest
class RiskRequest(BaseModel):
    commitCount: int
    linesChanged: int
    testPassRate: float
    hourOfDay: int
    dayOfWeek: int

# Response model matches RiskResponse with additional details
class RiskResponse(BaseModel):
    riskScore: float
    riskLevel: str
    details: Dict[str, float] = Field(default_factory=dict)

class SecurityScanRequest(BaseModel):
    files: List[str]
    content_snippets: List[str] # Simplified for prototype

class SecurityScanResponse(BaseModel):
    status: str
    vulnerabilities: List[Dict[str, str]]

def calculate_risk_score(request: RiskRequest) -> Tuple[float, Dict[str, float]]:
    """Calculate risk score using weighted feature analysis"""
    score = 0.0
    details = {}

    # Feature 1: Commit count (normalized, max impact 25%)
    commit_score = min(request.commitCount / 50.0, 1.0) * 0.25
    score += commit_score
    details["commitImpact"] = round(commit_score, 3)

    # Feature 2: Lines changed (normalized, max impact 30%)
    lines_score = min(request.linesChanged / 2000.0, 1.0) * 0.30
    score += lines_score
    details["sizeImpact"] = round(lines_score, 3)

    # Feature 3: Test pass rate (inverse - lower pass rate = higher risk, max impact 25%)
    test_score = (1.0 - request.testPassRate) * 0.25
    score += test_score
    details["testImpact"] = round(test_score, 3)

    # Feature 4: Time-based risk (weekend or after-hours, max impact 20%)
    time_risk = 0.0
    if request.dayOfWeek in [5, 6]:  # Saturday=5, Sunday=6
        time_risk += 0.10
    if request.hourOfDay < 8 or request.hourOfDay > 18:  # After hours
        time_risk += 0.10

    score += time_risk
    details["timeImpact"] = round(time_risk, 3)

    # Cap score at 1.0
    score = min(score, 1.0)
    details["finalScore"] = round(score, 3)

    return score, details

# Predict deployment risk based on metrics
@app.post("/predict", response_model=RiskResponse)
def predict(request: RiskRequest):
    score, details = calculate_risk_score(request)

    # Determine risk level
    if score < 0.3:
        level = "LOW"
    elif score < 0.5:
        level = "MEDIUM"
    elif score < 0.8:
        level = "HIGH"
    else:
        level = "CRITICAL"

    return RiskResponse(
        riskScore=round(score, 3),
        riskLevel=level,
        details=details
    )

@app.post("/models/upload")
async def upload_model(file: UploadFile = File(...), name: Optional[str] = None, type: str = "ml-model"):
    """
    Upload a trained ML model file.
    - Currently not tested
    """
    try:
        filename = f"{name}_{file.filename}" if name else file.filename
        file_path = os.path.join(MODELS_DIR, filename)
        
        with open(file_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)
            
        return {
            "message": f"Model '{filename}' uploaded successfully",
            "path": file_path,
            "type": type
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/health")
def health():
    """Health check endpoint"""
    return {
        "status": "healthy",
        "service": "ml-risk-service",
        "version": "1.1.0",
        "features": ["risk-scoring", "security-scan-stub"]
    }

@app.post("/security/scan", response_model=SecurityScanResponse)
def security_scan(request: Optional[SecurityScanRequest] = None):
    """
    Simulated security scan. 
    - Use tools like Bandit, Semgrep, or CodeQL in future
    """
    vulnerabilities = []
    
    # Simple keyword scanning simulation
    if request is not None:
        for i, content in enumerate(request.content_snippets):
            if "password" in content.lower() or "secret" in content.lower():
                vulnerabilities.append({
                    "type": "Hardcoded Secret",
                    "file": request.files[i] if i < len(request.files) else "unknown",
                    "severity": "HIGH",
                    "description": "Possible hardcoded secret detected"
                })
            if "eval(" in content:
                 vulnerabilities.append({
                    "type": "Dangerous Function",
                    "file": request.files[i] if i < len(request.files) else "unknown",
                    "severity": "CRITICAL",
                    "description": "Use of eval() detected"
                })

    return {
        "status": "completed",
        "vulnerabilities": vulnerabilities
    }

@app.post("/bugs/detect")
def bug_detection():
    """Placeholder for future bug detection"""
    return {
        "message": "Bug detection not implemented yet",
        "status": "pending",
        "bugs": []
    }