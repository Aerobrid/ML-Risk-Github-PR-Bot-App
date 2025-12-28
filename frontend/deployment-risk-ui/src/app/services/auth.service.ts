import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { User, AuthResponse } from '../models/user.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly currentUser = signal<User | null>(null);
  private readonly isAuthenticated = signal(false);

  readonly user = this.currentUser.asReadonly();
  readonly authenticated = this.isAuthenticated.asReadonly();

  constructor() {
    this.loadUserFromStorage();
  }

  initiateGitHubLogin(): void {
    const clientId = environment.githubClientId;
    const redirectUri = `${window.location.origin}/auth/callback`;
    const scope = 'read:user user:email';
    const state = this.generateRandomState();

    sessionStorage.setItem('oauth_state', state);

    const authUrl = `https://github.com/login/oauth/authorize?client_id=${clientId}&redirect_uri=${redirectUri}&scope=${scope}&state=${state}`;
    window.location.href = authUrl;
  }

  handleCallback(code: string, state: string): Observable<AuthResponse> {
    const savedState = sessionStorage.getItem('oauth_state');

    if (state !== savedState) {
      throw new Error('Invalid OAuth state');
    }

    const redirectUri = `${window.location.origin}/auth/callback`;

    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/github/callback`, { 
      code,
      redirectUri 
    }).pipe(
      tap(response => {
        this.setSession(response);
      })
    );
  }

  logout(): void {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('current_user');
    sessionStorage.removeItem('oauth_state');
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem('auth_token');
  }

  private setSession(authResponse: AuthResponse): void {
    localStorage.setItem('auth_token', authResponse.token);
    localStorage.setItem('current_user', JSON.stringify(authResponse.user));
    this.currentUser.set(authResponse.user);
    this.isAuthenticated.set(true);
  }

  private loadUserFromStorage(): void {
    const token = localStorage.getItem('auth_token');
    const userJson = localStorage.getItem('current_user');

    if (token && userJson) {
      try {
        const user = JSON.parse(userJson);
        this.currentUser.set(user);
        this.isAuthenticated.set(true);
      } catch {
        this.logout();
      }
    }
  }

  private generateRandomState(): string {
    return Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
  }
}
