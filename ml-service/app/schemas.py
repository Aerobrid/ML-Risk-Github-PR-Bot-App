from pydantic import BaseModel
from typing import Dict, List, Optional

class FileChange(BaseModel):
    filename: str
    patch: Optional[str] = None
    status: str # added, modified, removed

class RiskRequest(BaseModel):
    commitCount: int
    linesChanged: int
    testPassRate: float
    hourOfDay: int
    dayOfWeek: int
    files: List[FileChange] = []

class Vulnerability(BaseModel):
    type: str
    file: str
    severity: str
    description: str
    line: Optional[int] = None

class RiskResponse(BaseModel):
    riskScore: float
    riskLevel: str
    details: Dict[str, float] = {}
    scanReport: List[Vulnerability] = []

class ModelInfo(BaseModel):
    name: str
    type: str
    size_bytes: int
    active: bool = False

class SecurityScanRequest(BaseModel):
    files: List[str]
    content_snippets: List[str]

class SecurityScanResponse(BaseModel):
    status: str
    vulnerabilities: List[Vulnerability]