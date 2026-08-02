import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { User } from '../../shared/models';

interface SessionResponse {
  token?: string;
  accessToken?: string;
  user?: User;
  data?: { token?: string; accessToken?: string; user?: User };
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly storageKey = 'scrumboard.session';
  readonly user = signal<User | null>(this.readUser());

  constructor(
    private readonly http: HttpClient,
    private readonly config: RuntimeConfigService,
    private readonly router: Router
  ) {}

  login(email: string, password: string): Observable<SessionResponse> {
    return this.http.post<SessionResponse>(this.config.endpoint('sessions'), { email, password }).pipe(
      tap(response => {
        const payload = response.data ?? response;
        const token = payload.accessToken ?? payload.token;
        if (!token) throw new Error('La sesión no devolvió un token');
        const user = payload.user ?? this.userFromToken(token);
        sessionStorage.setItem(this.storageKey, JSON.stringify({ token, user }));
        this.user.set(user);
      })
    );
  }

  token(): string | null {
    try { return JSON.parse(sessionStorage.getItem(this.storageKey) ?? '{}').token ?? null; }
    catch { return null; }
  }

  isAuthenticated(): boolean {
    const token = this.token();
    if (!token) return false;
    const payload = this.decodeToken(token);
    return !payload?.['exp'] || Number(payload['exp']) * 1000 > Date.now();
  }

  logout(redirect = true): void {
    sessionStorage.removeItem(this.storageKey);
    this.user.set(null);
    if (redirect) void this.router.navigate(['/login']);
  }

  private readUser(): User | null {
    try { return JSON.parse(sessionStorage.getItem(this.storageKey) ?? '{}').user ?? null; }
    catch { return null; }
  }

  private userFromToken(token: string): User {
    const claims = this.decodeToken(token) ?? {};
    return {
      id: String(claims['sub'] ?? claims['nameid'] ?? ''),
      name: String(claims['name'] ?? claims['unique_name'] ?? claims['email'] ?? 'Usuario'),
      email: claims['email'] ? String(claims['email']) : undefined
    };
  }

  private decodeToken(token: string): Record<string, unknown> | null {
    try {
      const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      return JSON.parse(decodeURIComponent(atob(payload).split('').map(c => `%${c.charCodeAt(0).toString(16).padStart(2, '0')}`).join('')));
    } catch { return null; }
  }
}
