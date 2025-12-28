import { Component, signal, computed, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { RiskAssessment, RiskStatistics } from '../../models/risk-assessment.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="container mx-auto px-4 py-8">
      <div class="mb-8">
        <h1 class="text-3xl font-bold text-gray-900">Risk Assessment Dashboard</h1>
        <p class="text-gray-600 mt-2">Overview of deployment risks across your repositories</p>
      </div>

      @if (loading()) {
        <div class="flex justify-center items-center py-12">
          <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        </div>
      } @else if (error()) {
        <div class="bg-red-50 border border-red-200 rounded-lg p-4 mb-6">
          <p class="text-red-800">{{ error() }}</p>
          <p class="text-sm text-red-600 mt-2">
            @if (!stats()?.total) {
              Database may not be configured. Check Settings → Database Configuration.
            }
          </p>
        </div>
      } @else {
        <!-- Statistics Cards -->
        @if (stats(); as statistics) {
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4 mb-8">
            <div class="bg-white rounded-lg shadow p-6 border-l-4 border-blue-500">
              <h3 class="text-sm font-medium text-gray-600">Total Assessments</h3>
              <p class="text-3xl font-bold text-gray-900 mt-2">{{ statistics.total }}</p>
            </div>

            <div class="bg-green-50 rounded-lg shadow p-6 border-l-4 border-green-500">
              <h3 class="text-sm font-medium text-green-800">Low Risk</h3>
              <p class="text-3xl font-bold text-green-900 mt-2">{{ statistics.lowRisk }}</p>
              <p class="text-xs text-green-700 mt-1">{{ getPercentage(statistics.lowRisk, statistics.total) }}%</p>
            </div>

            <div class="bg-yellow-50 rounded-lg shadow p-6 border-l-4 border-yellow-500">
              <h3 class="text-sm font-medium text-yellow-800">Medium Risk</h3>
              <p class="text-3xl font-bold text-yellow-900 mt-2">{{ statistics.mediumRisk }}</p>
              <p class="text-xs text-yellow-700 mt-1">{{ getPercentage(statistics.mediumRisk, statistics.total) }}%</p>
            </div>

            <div class="bg-orange-50 rounded-lg shadow p-6 border-l-4 border-orange-500">
              <h3 class="text-sm font-medium text-orange-800">High Risk</h3>
              <p class="text-3xl font-bold text-orange-900 mt-2">{{ statistics.highRisk }}</p>
              <p class="text-xs text-orange-700 mt-1">{{ getPercentage(statistics.highRisk, statistics.total) }}%</p>
            </div>

            <div class="bg-red-50 rounded-lg shadow p-6 border-l-4 border-red-500">
              <h3 class="text-sm font-medium text-red-800">Critical Risk</h3>
              <p class="text-3xl font-bold text-red-900 mt-2">{{ statistics.criticalRisk }}</p>
              <p class="text-xs text-red-700 mt-1">{{ getPercentage(statistics.criticalRisk, statistics.total) }}%</p>
            </div>
          </div>
        }

        <!-- Assessments Table -->
        <div class="bg-white shadow-lg rounded-lg overflow-hidden">
          <div class="px-6 py-4 border-b border-gray-200">
            <h2 class="text-xl font-semibold text-gray-800">Recent Assessments</h2>
          </div>

          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-gray-200">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Repository</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Event</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Risk Level</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Score</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Author</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
                </tr>
              </thead>
              <tbody class="bg-white divide-y divide-gray-200">
                @for (assessment of assessments(); track assessment.id) {
                  <tr class="hover:bg-gray-50">
                    <td class="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
                      {{ assessment.repositoryFullName }}
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                      @if (assessment.pullRequestNumber) {
                        <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
                          PR #{{ assessment.pullRequestNumber }}
                        </span>
                      } @else {
                        <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-purple-100 text-purple-800">
                          Push → {{ assessment.branch }}
                        </span>
                      }
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap">
                      <span [class]="getRiskLevelClass(assessment.riskLevel)">
                        {{ assessment.riskLevel }}
                      </span>
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                      {{ (assessment.overallRiskScore * 100).toFixed(0) }}%
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                      {{ assessment.author }}
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                      {{ formatDate(assessment.createdAt) }}
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap text-sm">
                      @if (assessment.gitHubCommentUrl) {
                        <a
                          [href]="assessment.gitHubCommentUrl"
                          target="_blank"
                          class="text-blue-600 hover:text-blue-900 font-medium"
                        >
                          View on GitHub →
                        </a>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="px-6 py-8 text-center text-gray-500">
                      No risk assessments found. Create a PR in a monitored repository to see assessments.
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </div>
  `,
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly assessments = signal<RiskAssessment[]>([]);
  protected readonly stats = signal<RiskStatistics | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.getStatistics().subscribe({
      next: (statistics) => {
        this.stats.set(statistics);
      },
      error: (err) => {
        console.error('Error loading statistics:', err);
      },
    });

    this.api.getRecentAssessments().subscribe({
      next: (data) => {
        this.assessments.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading assessments:', err);
        this.error.set('Failed to load risk assessments. Please check your configuration.');
        this.loading.set(false);
      },
    });
  }

  protected getRiskLevelClass(level: string): string {
    const baseClass = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium';
    switch (level) {
      case 'LOW':
        return `${baseClass} bg-green-100 text-green-800`;
      case 'MEDIUM':
        return `${baseClass} bg-yellow-100 text-yellow-800`;
      case 'HIGH':
        return `${baseClass} bg-orange-100 text-orange-800`;
      case 'CRITICAL':
        return `${baseClass} bg-red-100 text-red-800`;
      default:
        return `${baseClass} bg-gray-100 text-gray-800`;
    }
  }

  protected formatDate(date: string): string {
    return new Date(date).toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  protected getPercentage(value: number, total: number): number {
    return total > 0 ? Math.round((value / total) * 100) : 0;
  }
}
