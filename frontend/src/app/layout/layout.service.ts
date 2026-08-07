import { DOCUMENT } from '@angular/common';
import { Inject, Injectable, signal } from '@angular/core';

export interface AppMenuItem {
  label: string;
  icon: string;
  routerLink: string;
}

type AppTheme = 'light' | 'dark';

const themeStorageKey = 'scrumboard.theme';
const themeStylesheets: Record<AppTheme, string> = {
  light: 'assets/themes/lara-light-blue/theme.css',
  dark: 'assets/themes/lara-dark-blue/theme.css'
};

@Injectable({ providedIn: 'root' })
export class LayoutService {
  readonly mobileMenuOpen = signal(false);
  readonly darkTheme = signal(false);
  readonly mobile = signal(globalThis.matchMedia?.('(max-width: 800px)').matches ?? false);
  readonly menuItems: readonly AppMenuItem[] = [
    { label: 'Proyectos', icon: 'pi pi-th-large', routerLink: '/projects' }
  ];
  private themeInitialized = false;

  constructor(@Inject(DOCUMENT) private readonly document: Document) {
    const media = globalThis.matchMedia?.('(max-width: 800px)');
    media?.addEventListener('change', event => {
      this.mobile.set(event.matches);
      if (!event.matches) this.closeMenu();
    });
  }

  initializeTheme(): void {
    if (this.themeInitialized) return;
    this.themeInitialized = true;

    const stored = this.readStoredTheme();
    const prefersDark = this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
    this.applyTheme(stored ?? (prefersDark ? 'dark' : 'light'), false);
  }

  toggleMenu(): void {
    this.mobileMenuOpen.update(open => !open);
  }

  closeMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  toggleTheme(): void {
    this.initializeTheme();
    this.applyTheme(this.darkTheme() ? 'light' : 'dark', true);
  }

  private applyTheme(theme: AppTheme, persist: boolean): void {
    const dark = theme === 'dark';
    this.darkTheme.set(dark);
    this.document.documentElement.classList.toggle('app-dark', dark);
    this.themeLink().setAttribute('href', themeStylesheets[theme]);

    if (!persist) return;
    try {
      this.document.defaultView?.localStorage.setItem(themeStorageKey, theme);
    } catch {
      // Theme selection still works when browser storage is unavailable.
    }
  }

  private readStoredTheme(): AppTheme | null {
    try {
      const stored = this.document.defaultView?.localStorage.getItem(themeStorageKey);
      return stored === 'light' || stored === 'dark' ? stored : null;
    } catch {
      return null;
    }
  }

  private themeLink(): HTMLLinkElement {
    const existing = this.document.getElementById('app-theme');
    if (existing instanceof HTMLLinkElement) return existing;

    const link = this.document.createElement('link');
    link.id = 'app-theme';
    link.rel = 'stylesheet';
    this.document.head.append(link);
    return link;
  }
}
