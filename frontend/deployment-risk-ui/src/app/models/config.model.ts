export interface DatabaseConfig {
  connectionString: string | null;
  isConfigured: boolean;
}

export interface ScoringConfig {
  enabled: {
    ruleBased: boolean;
    mlModel: boolean;
    securityScan: boolean;
    bugDetection: boolean;
  };
  weights: {
    ruleBased: number;
    mlModel: number;
    securityScan: number;
    bugDetection: number;
  };
}

export interface MLModelConfig {
  id: string;
  name: string;
  endpoint: string;
  enabled: boolean;
  type: 'rule-based' | 'ml-model' | 'security' | 'bug-detection';
  uploadedAt?: string;
  version?: string;
}

export interface GitHubRepository {
  id: number;
  name: string;
  fullName: string;
  owner: string;
  private: boolean;
  installationId: number;
  assessmentCount?: number;
  lastAssessment?: string;
}
