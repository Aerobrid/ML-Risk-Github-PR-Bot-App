export interface User {
  id: number;
  login: string;
  name: string;
  email?: string;
  avatarUrl: string;
  installationId?: number;
}

export interface AuthResponse {
  token: string;
  user: User;
}
