import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gray-50">
      <div class="text-center">
        @if (error()) {
          <div class="bg-red-50 border border-red-200 rounded-lg p-6 max-w-md">
            <h2 class="text-xl font-semibold text-red-900 mb-2">Authentication Failed</h2>
            <p class="text-red-700">{{ error() }}</p>
            <button
              (click)="returnToLogin()"
              class="mt-4 px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
            >
              Back to Login
            </button>
          </div>
        } @else {
          <div class="animate-pulse">
            <div class="w-16 h-16 bg-blue-500 rounded-full mx-auto mb-4"></div>
            <p class="text-gray-600">Completing authentication...</p>
          </div>
        }
      </div>
    </div>
  `,
})
export class AuthCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    const state = this.route.snapshot.queryParamMap.get('state');
    const errorParam = this.route.snapshot.queryParamMap.get('error');

    if (errorParam) {
      this.error.set('GitHub authentication was cancelled or failed.');
      return;
    }

    if (!code || !state) {
      this.error.set('Invalid OAuth callback parameters.');
      return;
    }

    this.authService.handleCallback(code, state).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        console.error('Auth callback error:', err);
        let errorMessage = 'Failed to complete authentication. Please try again.';
        
        if (err.error?.message) {
          errorMessage = err.error.message;
        } else if (err.message) {
          errorMessage = err.message;
        } else if (err.status === 0) {
          errorMessage = 'Cannot connect to the backend server. Please make sure the backend is running on http://localhost:5000';
        } else if (err.status === 401) {
          errorMessage = err.error?.message || 'Authentication failed. Please check your GitHub OAuth app configuration.';
        } else if (err.status === 500) {
          errorMessage = err.error?.message || 'Server error during authentication. Please try again.';
        }
        
        this.error.set(errorMessage);
      },
    });
  }

  protected returnToLogin(): void {
    this.router.navigate(['/login']);
  }
}
