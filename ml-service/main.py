from fastapi import FastAPI
from pydantic import BaseModel
import random

app = FastAPI(title="ML Risk Service")

# Request model matches ASP.NET RiskRequest
class RiskRequest(BaseModel):
    commitCount: int
    linesChanged: int
    testPassRate: float
    hourOfDay: int
    dayOfWeek: int

# Response model matches RiskResponse
class RiskResponse(BaseModel):
    riskScore: float
    riskLevel: str

@app.post("/predict", response_model=RiskResponse)
def predict(request: RiskRequest):
    # placeholder random risk logic
    score = random.random()
    if score < 0.3:
        level = "LOW"
    elif score < 0.7:
        level = "MEDIUM"
    else:
        level = "HIGH"

    return RiskResponse(riskScore=score, riskLevel=level)
