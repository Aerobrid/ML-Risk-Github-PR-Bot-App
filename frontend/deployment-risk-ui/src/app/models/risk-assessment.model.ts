export interface RiskAssessment {
  id: string;
  repositoryFullName: string;
  eventType: string;
  pullRequestNumber?: number;
  sha: string;
  branch: string;
  overallRiskScore: number;
  riskLevel: string;
  ruleBasedScore?: number;
  mlScore?: number;
  securityScore?: number;
  bugScore?: number;
  createdAt: string;
  gitHubCommentUrl?: string;
  author: string;
  riskFactorsJson?: string;
  metricsJson?: string;
}

export interface RiskStatistics {
  total: number;
  lowRisk: number;
  mediumRisk: number;
  highRisk: number;
  criticalRisk: number;
  averageScore: number;
}

export interface RiskFactor {
  description: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
}
