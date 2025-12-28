import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { RiskAssessment, RiskStatistics } from '../models/risk-assessment.model';
import { DatabaseConfig, ScoringConfig, MLModelConfig, GitHubRepository } from '../models/config.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // Dashboard endpoints
  getRecentAssessments(count = 100): Observable<RiskAssessment[]> {
    return this.http.get<RiskAssessment[]>(`${this.baseUrl}/dashboard/assessments?count=${count}`);
  }

  getRepositoryAssessments(repoFullName: string, pageSize = 50): Observable<RiskAssessment[]> {
    return this.http.get<RiskAssessment[]>(`${this.baseUrl}/dashboard/assessments/${repoFullName}?pageSize=${pageSize}`);
  }

  getStatistics(): Observable<RiskStatistics> {
    return this.http.get<RiskStatistics>(`${this.baseUrl}/dashboard/stats`);
  }

  // Configuration endpoints
  getDatabaseConfig(): Observable<DatabaseConfig> {
    return this.http.get<DatabaseConfig>(`${this.baseUrl}/config/database`);
  }

  updateDatabaseConfig(connectionString: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/config/database`, { connectionString });
  }

  getScoringConfig(): Observable<ScoringConfig> {
    return this.http.get<ScoringConfig>(`${this.baseUrl}/config/scoring`);
  }

  updateScoringConfig(config: ScoringConfig): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/config/scoring`, config);
  }

  // ML Model endpoints
  getMLModels(): Observable<MLModelConfig[]> {
    return this.http.get<MLModelConfig[]>(`${this.baseUrl}/ml/models`);
  }

  uploadMLModel(file: File, name: string, type: string): Observable<MLModelConfig> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('name', name);
    formData.append('type', type);

    return this.http.post<MLModelConfig>(`${this.baseUrl}/ml/models/upload`, formData);
  }

  updateMLModelEndpoint(modelId: string, endpoint: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/ml/models/${modelId}/endpoint`, { endpoint });
  }

  deleteMLModel(modelId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/ml/models/${modelId}`);
  }

  // GitHub repositories
  getInstalledRepositories(): Observable<GitHubRepository[]> {
    return this.http.get<GitHubRepository[]>(`${this.baseUrl}/github/repositories`);
  }

  // Health check
  healthCheck(): Observable<{ status: string }> {
    return this.http.get<{ status: string }>(`${this.baseUrl}/health`);
  }
}
