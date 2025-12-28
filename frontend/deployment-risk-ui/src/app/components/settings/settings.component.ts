import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { DatabaseConfig, ScoringConfig, MLModelConfig, GitHubRepository } from '../../models/config.model';

type TabType = 'database' | 'github' | 'scoring';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="container mx-auto px-4 py-8">
      <div class="mb-8">
        <h1 class="text-3xl font-bold text-gray-900">Settings</h1>
        <p class="text-gray-600 mt-2">Configure your deployment risk platform</p>
      </div>

      <!-- Tabs -->
      <div class="border-b border-gray-200 mb-6">
        <nav class="-mb-px flex space-x-8">
          <button
            (click)="activeTab.set('database')"
            [class]="getTabClass('database')"
          >
            Database
          </button>
          <button
            (click)="activeTab.set('github')"
            [class]="getTabClass('github')"
          >
            GitHub Repos
          </button>
          <button
            (click)="activeTab.set('scoring')"
            [class]="getTabClass('scoring')"
          >
            Risk Scoring
          </button>
        </nav>
      </div>

      <!-- Tab Content -->
      <div class="bg-white shadow rounded-lg p-6">
        @switch (activeTab()) {
          @case ('database') {
            <div>
              <h2 class="text-xl font-semibold text-gray-900 mb-4">Database Configuration</h2>
              <p class="text-sm text-gray-600 mb-6">
                Configure SQL Server connection for persisting risk assessments. Leave empty to use GitHub comment-only mode.
              </p>

              <div class="space-y-4">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-2">
                    SQL Server Connection String
                  </label>
                  <input
                    type="text"
                    [(ngModel)]="connectionString"
                    placeholder="Server=localhost;Database=RiskDb;User Id=sa;Password=***;TrustServerCertificate=True"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                  <p class="text-xs text-gray-500 mt-2">
                    Current status:
                    @if (dbConfig()?.isConfigured) {
                      <span class="text-green-600 font-medium">✓ Connected</span>
                    } @else {
                      <span class="text-orange-600 font-medium">⚠ GitHub-only mode</span>
                    }
                  </p>
                </div>

                <button
                  (click)="saveDatabaseConfig()"
                  [disabled]="savingDb()"
                  class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
                >
                  @if (savingDb()) {
                    Saving...
                  } @else {
                    Save Database Config
                  }
                </button>
              </div>
            </div>
          }
          @case ('github') {
            <div>
              <h2 class="text-xl font-semibold text-gray-900 mb-4">GitHub Repositories</h2>
              <p class="text-sm text-gray-600 mb-6">
                Repositories where the GitHub App is installed. To add more repositories, go to your
                <a href="https://github.com/settings/installations" target="_blank" class="text-blue-600 hover:underline">
                  GitHub App installations
                </a>.
              </p>

              @if (loadingRepos()) {
                <div class="flex justify-center py-8">
                  <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
                </div>
              } @else {
                <div class="space-y-3">
                  @for (repo of repositories(); track repo.id) {
                    <div class="border border-gray-200 rounded-lg p-4 hover:border-blue-300 transition-colors">
                      <div class="flex items-center justify-between">
                        <div>
                          <h3 class="font-medium text-gray-900">{{ repo.fullName }}</h3>
                          <p class="text-sm text-gray-500 mt-1">
                            @if (repo.assessmentCount) {
                              {{ repo.assessmentCount }} assessments
                            } @else {
                              No assessments yet
                            }
                          </p>
                        </div>
                        @if (repo.private) {
                          <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                            Private
                          </span>
                        }
                      </div>
                    </div>
                  } @empty {
                    <p class="text-center text-gray-500 py-8">
                      No repositories found. Install the GitHub App on your repositories to get started.
                    </p>
                  }
                </div>
              }
            </div>
          }
          @case ('scoring') {
            <div>
              <h2 class="text-xl font-semibold text-gray-900 mb-4">Risk Scoring Configuration</h2>
              <p class="text-sm text-gray-600 mb-6">
                Configure how risk scores are calculated. Adjust scorer weights based on model maturity.
              </p>

              <!-- Info box about ML model training -->
              <div class="mb-6 p-4 bg-blue-50 border border-blue-200 rounded-lg">
                <h3 class="font-medium text-blue-900 mb-2">📘 About ML Model Training</h3>
                <p class="text-sm text-blue-800 mb-2">
                  The main XGBoost risk scoring model is trained via CLI, not through this UI.
                </p>
                <div class="text-xs text-blue-700 space-y-1">
                  <p><strong>To train the model:</strong></p>
                  <code class="block bg-blue-100 px-2 py-1 rounded mt-1">cd ml-service && python train_xgboost_model.py</code>
                  <p class="mt-2"><strong>To retrain with real data:</strong></p>
                  <code class="block bg-blue-100 px-2 py-1 rounded mt-1">make collect-data REPO=your-org/repo LIMIT=1000</code>
                  <code class="block bg-blue-100 px-2 py-1 rounded mt-1">make train</code>
                </div>
              </div>

              @if (scoringConfig(); as config) {
                <div class="space-y-6">
                  <!-- Rule-Based Scorer -->
                  <div class="border border-gray-200 rounded-lg p-4">
                    <div class="flex items-center justify-between mb-3">
                      <div>
                        <h3 class="font-medium text-gray-900">Rule-Based Scorer</h3>
                        <p class="text-sm text-gray-500">Deterministic rules (commit count, size, timing, critical files)</p>
                        <p class="text-xs text-gray-400 mt-1">💡 Use higher weight (0.7) during cold start</p>
                      </div>
                      <label class="flex items-center">
                        <input
                          type="checkbox"
                          [(ngModel)]="config.enabled.ruleBased"
                          class="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                        />
                        <span class="ml-2 text-sm text-gray-700">Enabled</span>
                      </label>
                    </div>
                    <div>
                      <label class="block text-sm text-gray-700 mb-2">Weight: {{ config.weights.ruleBased }}</label>
                      <input
                        type="range"
                        [(ngModel)]="config.weights.ruleBased"
                        min="0"
                        max="1"
                        step="0.1"
                        class="w-full"
                      />
                    </div>
                  </div>

                  <!-- ML Model Scorer -->
                  <div class="border border-gray-200 rounded-lg p-4 bg-gray-50">
                    <div class="flex items-center justify-between mb-3">
                      <div>
                        <h3 class="font-medium text-gray-900">ML Model Scorer (XGBoost)</h3>
                        <p class="text-sm text-gray-500">XGBoost model trained on historical PR data</p>
                        <p class="text-xs text-gray-400 mt-1">💡 Increase weight (0.7) after collecting real data</p>
                      </div>
                      <label class="flex items-center">
                        <input
                          type="checkbox"
                          [(ngModel)]="config.enabled.mlModel"
                          class="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                        />
                        <span class="ml-2 text-sm text-gray-700">Enabled</span>
                      </label>
                    </div>
                    <div>
                      <label class="block text-sm text-gray-700 mb-2">Weight: {{ config.weights.mlModel }}</label>
                      <input
                        type="range"
                        [(ngModel)]="config.weights.mlModel"
                        min="0"
                        max="1"
                        step="0.1"
                        class="w-full"
                      />
                    </div>
                  </div>

                  <!-- Security Scanner -->
                  <div class="border border-gray-200 rounded-lg p-4 bg-gray-50">
                    <div class="flex items-center justify-between mb-3">
                      <div>
                        <h3 class="font-medium text-gray-900">Security Scanner</h3>
                        <p class="text-sm text-gray-500">Vulnerability detection (coming soon)</p>
                      </div>
                      <label class="flex items-center">
                        <input
                          type="checkbox"
                          [(ngModel)]="config.enabled.securityScan"
                          disabled
                          class="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                        />
                        <span class="ml-2 text-sm text-gray-400">Enabled</span>
                      </label>
                    </div>
                  </div>

                  <!-- Bug Detection -->
                  <div class="border border-gray-200 rounded-lg p-4 bg-gray-50">
                    <div class="flex items-center justify-between mb-3">
                      <div>
                        <h3 class="font-medium text-gray-900">Bug Detection</h3>
                        <p class="text-sm text-gray-500">Static analysis for common bugs (coming soon)</p>
                      </div>
                      <label class="flex items-center">
                        <input
                          type="checkbox"
                          [(ngModel)]="config.enabled.bugDetection"
                          disabled
                          class="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                        />
                        <span class="ml-2 text-sm text-gray-400">Enabled</span>
                      </label>
                    </div>
                  </div>

                  <button
                    (click)="saveScoringConfig()"
                    [disabled]="savingScoring()"
                    class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-400"
                  >
                    @if (savingScoring()) {
                      Saving...
                    } @else {
                      Save Scoring Configuration
                    }
                  </button>
                </div>
              }
            </div>
          }
        }

        <!-- Success/Error Messages -->
        @if (message()) {
          <div [class]="messageClass()" class="mt-6 p-4 rounded-lg">
            {{ message() }}
          </div>
        }
      </div>
    </div>
  `,
})
export class SettingsComponent implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly activeTab = signal<TabType>('database');
  protected readonly message = signal<string | null>(null);
  protected readonly messageClass = signal('');

  // Database
  protected readonly dbConfig = signal<DatabaseConfig | null>(null);
  protected connectionString = '';
  protected readonly savingDb = signal(false);

  // GitHub
  protected readonly repositories = signal<GitHubRepository[]>([]);
  protected readonly loadingRepos = signal(false);

  // Scoring
  protected readonly scoringConfig = signal<ScoringConfig | null>(null);
  protected readonly savingScoring = signal(false);

  ngOnInit(): void {
    this.loadDatabaseConfig();
    this.loadScoringConfig();
    this.loadRepositories();
  }

  protected getTabClass(tab: TabType): string {
    const baseClass = 'whitespace-nowrap py-4 px-1 border-b-2 font-medium text-sm';
    return this.activeTab() === tab
      ? `${baseClass} border-blue-500 text-blue-600`
      : `${baseClass} border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300`;
  }

  // Database methods
  private loadDatabaseConfig(): void {
    this.api.getDatabaseConfig().subscribe({
      next: (config) => {
        this.dbConfig.set(config);
        this.connectionString = config.connectionString || '';
      },
      error: (err) => console.error('Error loading database config:', err),
    });
  }

  protected saveDatabaseConfig(): void {
    this.savingDb.set(true);
    this.api.updateDatabaseConfig(this.connectionString).subscribe({
      next: (response) => {
        this.showMessage(response.message, 'success');
        this.savingDb.set(false);
        this.loadDatabaseConfig();
      },
      error: (err) => {
        this.showMessage('Failed to save database configuration', 'error');
        this.savingDb.set(false);
      },
    });
  }

  // GitHub methods
  private loadRepositories(): void {
    this.loadingRepos.set(true);
    this.api.getInstalledRepositories().subscribe({
      next: (repos) => {
        this.repositories.set(repos);
        this.loadingRepos.set(false);
      },
      error: (err) => {
        console.error('Error loading repositories:', err);
        this.loadingRepos.set(false);
      },
    });
  }

  // Scoring methods
  private loadScoringConfig(): void {
    this.api.getScoringConfig().subscribe({
      next: (config) => this.scoringConfig.set(config),
      error: (err) => console.error('Error loading scoring config:', err),
    });
  }

  protected saveScoringConfig(): void {
    const config = this.scoringConfig();
    if (!config) return;

    this.savingScoring.set(true);
    this.api.updateScoringConfig(config).subscribe({
      next: (response) => {
        this.showMessage(response.message, 'success');
        this.savingScoring.set(false);
      },
      error: (err) => {
        this.showMessage('Failed to save scoring configuration', 'error');
        this.savingScoring.set(false);
      },
    });
  }

  private showMessage(msg: string, type: 'success' | 'error'): void {
    this.message.set(msg);
    this.messageClass.set(
      type === 'success'
        ? 'bg-green-50 text-green-800 border border-green-200'
        : 'bg-red-50 text-red-800 border border-red-200'
    );

    setTimeout(() => {
      this.message.set(null);
    }, 5000);
  }
}
