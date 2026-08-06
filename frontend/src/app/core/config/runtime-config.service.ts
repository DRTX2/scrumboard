import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface AppConfig {
  apiBaseUrl: string;
  hubUrl: string;
  endpoints: Record<string, string>;
}

@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private value?: AppConfig;

  constructor(private readonly http: HttpClient) {}

  async load(): Promise<void> {
    this.value = await firstValueFrom(this.http.get<AppConfig>('assets/app-config.json'));
  }

  setForTesting(config: AppConfig): void {
    this.value = config;
  }

  get config(): AppConfig {
    if (!this.value) throw new Error('Runtime configuration is not loaded');
    return this.value;
  }

  endpoint(name: string, params: Record<string, string> = {}): string {
    const route = this.config.endpoints[name];
    if (!route) throw new Error(`Endpoint not configured: ${name}`);
    const path = Object.entries(params).reduce(
      (result, [key, value]) => result.replace(`{${key}}`, encodeURIComponent(value)), route
    );
    return `${this.config.apiBaseUrl.replace(/\/$/, '')}/${path.replace(/^\//, '')}`;
  }

  isApiUrl(url: string): boolean {
    if (!this.value || url.startsWith('assets/') || url.startsWith('/assets/')) return false;
    try {
      const origin = globalThis.location?.origin ?? 'http://localhost';
      const requestUrl = new URL(url, origin);
      const apiUrl = new URL(this.value.apiBaseUrl, origin);
      const apiPath = apiUrl.pathname.replace(/\/$/, '');
      return requestUrl.origin === apiUrl.origin &&
        (requestUrl.pathname === apiPath || requestUrl.pathname.startsWith(`${apiPath}/`));
    } catch {
      return false;
    }
  }

  isEndpointUrl(name: string, url: string): boolean {
    if (!this.value) return false;
    try {
      const origin = globalThis.location?.origin ?? 'http://localhost';
      return new URL(url, origin).href === new URL(this.endpoint(name), origin).href;
    } catch {
      return false;
    }
  }

  get hubUrl(): string {
    if (/^https?:\/\//i.test(this.config.hubUrl)) return this.config.hubUrl;
    return `${this.config.apiBaseUrl.replace(/\/api\/?$/, '')}/${this.config.hubUrl.replace(/^\//, '')}`;
  }
}
