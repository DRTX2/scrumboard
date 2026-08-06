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
  private handlingUnauthorized = false;
  readonly user = signal<User | null>(null);

  constructor(
    private readonly http: HttpClient,
    private readonly config: RuntimeConfigService,
    private readonly router: Router
  ) {
    this.restoreSession();
  }

  login(email: string, password: string): Observable<SessionResponse> {
    return this.http.post<SessionResponse>(this.config.endpoint('sessions'), { email, password }).pipe(
      tap(response => {
        const payload = response.data ?? response;
        const token = payload.accessToken ?? payload.token;
        if (!token || !this.validPayload(token)) {
          this.clearSession();
          throw new Error('El servidor devolvió una sesión no válida.');
        }
        const tokenUser = this.userFromToken(token);
        const user = payload.user?.id === tokenUser.id
          ? { ...tokenUser, ...payload.user, id: tokenUser.id }
          : tokenUser;
        sessionStorage.setItem(this.storageKey, JSON.stringify({ token, user }));
        this.user.set(user);
        this.handlingUnauthorized = false;
      })
    );
  }

  token(): string | null {
    const session = this.readSession();
    if (!session) return null;
    if (!session.token) {
      this.clearSession();
      return null;
    }
    if (!this.validPayload(session.token)) {
      this.clearSession();
      return null;
    }
    return session.token;
  }

  isAuthenticated(): boolean {
    return this.token() !== null;
  }

  logout(redirect = true): void {
    this.clearSession();
    if (redirect) void this.router.navigate(['/login']);
  }

  expireSession(returnUrl: string): boolean {
    if (this.handlingUnauthorized) return false;
    this.handlingUnauthorized = true;
    this.clearSession();
    void this.router.navigate(['/login'], { queryParams: { returnUrl: safeInternalReturnUrl(returnUrl) } });
    return true;
  }

  private restoreSession(): void {
    const session = this.readSession();
    if (!session?.token || !this.validPayload(session.token)) {
      if (session) this.clearSession();
      return;
    }
    const tokenUser = this.userFromToken(session.token);
    this.user.set(session.user?.id === tokenUser.id
      ? { ...tokenUser, ...session.user, id: tokenUser.id }
      : tokenUser);
  }

  private readSession(): { token?: string; user?: User } | null {
    try {
      const value = JSON.parse(sessionStorage.getItem(this.storageKey) ?? 'null') as unknown;
      return value && typeof value === 'object' ? value as { token?: string; user?: User } : null;
    } catch {
      this.clearSession();
      return null;
    }
  }

  private clearSession(): void {
    sessionStorage.removeItem(this.storageKey);
    this.user.set(null);
  }

  private userFromToken(token: string): User {
    const claims = this.decodeToken(token)!;
    return {
      id: String(claims['sub'] ?? claims['nameid'] ?? ''),
      name: String(claims['name'] ?? claims['unique_name'] ?? claims['email'] ?? 'Usuario'),
      email: claims['email'] ? String(claims['email']) : undefined
    };
  }

  private decodeToken(token: string): Record<string, unknown> | null {
    try {
      const parts = token.split('.');
      if (parts.length !== 3 || !parts[1]) return null;
      const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(parts[1].length / 4) * 4, '=');
      const decoded = JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(payload), character => character.charCodeAt(0)))) as unknown;
      return decoded && typeof decoded === 'object' && !Array.isArray(decoded) ? decoded as Record<string, unknown> : null;
    } catch { return null; }
  }

  private validPayload(token: string): Record<string, unknown> | null {
    const payload = this.decodeToken(token);
    const subject = payload?.['sub'];
    const expiration = payload?.['exp'];
    return typeof subject === 'string' && subject.trim().length > 0 &&
      typeof expiration === 'number' && Number.isFinite(expiration) && expiration * 1000 > Date.now()
      ? payload
      : null;
  }
}

export function safeInternalReturnUrl(value: string | null | undefined): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return '/projects';
  try {
    const origin = globalThis.location?.origin ?? 'http://localhost';
    const url = new URL(value, origin);
    if (url.origin !== origin || url.pathname === '/login') return '/projects';
    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return '/projects';
  }
}
