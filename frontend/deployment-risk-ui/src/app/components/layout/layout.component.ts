import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink, RouterOutlet, Router, RouterLinkActive } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterLink, RouterOutlet, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Navigation -->
      <nav class="bg-white shadow-sm border-b border-gray-200">
        <div class="container mx-auto px-4">
          <div class="flex justify-between h-16">
            <!-- Logo and Links -->
            <div class="flex">
              <div class="shrink-0 flex items-center">
                <h1 class="text-xl font-bold text-gray-900">Deployment Risk Platform</h1>
              </div>
              <div class="hidden sm:ml-8 sm:flex sm:space-x-8">
                <a
                  routerLink="/dashboard"
                  routerLinkActive="border-blue-500 text-gray-900"
                  [routerLinkActiveOptions]="{exact: false}"
                  class="border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 inline-flex items-center px-1 pt-1 border-b-2 text-sm font-medium"
                >
                  Dashboard
                </a>
                <a
                  routerLink="/settings"
                  routerLinkActive="border-blue-500 text-gray-900"
                  class="border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 inline-flex items-center px-1 pt-1 border-b-2 text-sm font-medium"
                >
                  Settings
                </a>
              </div>
            </div>

            <!-- User Menu -->
            <div class="flex items-center">
              @if (authService.user(); as user) {
                <div class="flex items-center gap-4">
                  <div class="flex items-center gap-3">
                    <img
                      [src]="user.avatarUrl"
                      [alt]="user.login"
                      class="h-8 w-8 rounded-full"
                    />
                    <span class="text-sm font-medium text-gray-700">{{ user.login }}</span>
                  </div>
                  <button
                    (click)="logout()"
                    class="px-3 py-2 text-sm font-medium text-gray-700 hover:text-gray-900 hover:bg-gray-100 rounded-lg transition-colors"
                  >
                    Logout
                  </button>
                </div>
              }
            </div>
          </div>
        </div>
      </nav>

      <!-- Main Content -->
      <main class="flex-1">
        <router-outlet />
      </main>

      <!-- Footer -->
      <footer class="bg-white border-t border-gray-200 mt-12">
        <div class="container mx-auto px-4 py-6">
          <div class="flex justify-between items-center text-sm text-gray-600">
            <p>Deployment Risk Platform - Automated PR & Push Risk Assessment</p>
            <div class="flex gap-6">
              <a href="https://github.com" target="_blank" class="hover:text-gray-900">GitHub</a>
              <a [href]="swaggerUrl" target="_blank" class="hover:text-gray-900">API Docs</a>
            </div>
          </div>
        </div>
      </footer>
    </div>
  `,
})
export class LayoutComponent {
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  // go to actual swagger endpoint (on port 5000)
  protected readonly swaggerUrl = ((): string => {
    const api = environment.apiUrl ?? '';
    const base = api.replace(/\/api\/?$/i, '');
    return base.endsWith('/') ? base + 'swagger' : base + '/swagger';
  })();

  protected logout(): void {
    this.authService.logout();
  }
}

