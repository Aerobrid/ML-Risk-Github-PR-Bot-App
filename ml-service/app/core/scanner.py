import re
from typing import List
from app.schemas import FileChange, Vulnerability

class HeuristicScanner:
    def __init__(self):
        # Simple regex patterns for common secrets/issues
        self.patterns = [
            (r'AWS_ACCESS_KEY_ID\s*=\s*[\'\"]AKIA[0-9A-Z]{16}[\'\"]', "Secret", "CRITICAL", "Potential AWS Access Key ID"),
            (r'-----BEGIN PRIVATE KEY-----', "Secret", "CRITICAL", "Private Key found"),
            (r'password\s*=\s*[\'\"][^\'\"]{3,}[\'\"]', "Secret", "HIGH", "Potential hardcoded password"),
            (r'eval\(', "Security", "HIGH", "Use of eval() detected"),
            (r'exec\(', "Security", "HIGH", "Use of exec() detected"),
            (r'TODO:', "Quality", "LOW", "TODO comment found"),
            (r'FIXME:', "Quality", "MEDIUM", "FIXME comment found"),
            (r'console\.log\(', "Quality", "LOW", "Console log left in code"),
            (r'print\(', "Quality", "LOW", "Print statement left in code"),
        ]

    def scan(self, files: List[FileChange]) -> List[Vulnerability]:
        vulnerabilities = []
        
        for file in files:
            if not file.patch:
                continue
                
            # Scan the patch content
            for line_idx, line in enumerate(file.patch.split('\n')):
                # Check for patterns
                for pattern, type_, severity, desc in self.patterns:
                    if re.search(pattern, line, re.IGNORECASE):
                        # Filter out removals (lines starting with -)
                        if line.startswith('-'):
                            continue
                            
                        vulnerabilities.append(Vulnerability(
                            type=type_,
                            file=file.filename,
                            severity=severity,
                            description=desc,
                            line=line_idx + 1 # Approximate line in patch
                        ))
        
        return vulnerabilities

_scanner = HeuristicScanner()

def get_scanner():
    return _scanner
