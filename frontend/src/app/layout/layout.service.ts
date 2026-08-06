import { DOCUMENT } from '@angular/common';
import { Inject, Injectable, signal } from '@angular/core';

export interface AppMenuItem {
  label: string;
  icon: string;
  routerLink: string;
}

@Injectable({ providedIn: 'root' })
export class LayoutService {
  readonly mobileMenuOpen = signal(false);
  readonly darkTheme = signal(false);
  readonly mobile = signal(globalThis.matchMedia?.('(max-width: 800px)').matches ?? false);
  readonly menuItems: readonly AppMenuItem[] = [
    { label: 'Proyectos', icon: 'pi pi-th-large', routerLink: '/projects' }
  ];

  constructor(@Inject(DOCUMENT) private readonly document: Document) {
    const media = globalThis.matchMedia?.('(max-width: 800px)');
    media?.addEventListener('change', event => {
      this.mobile.set(event.matches);
      if (!event.matches) this.closeMenu();
    });
  }

  toggleMenu(): void {
    this.mobileMenuOpen.update(open => !open);
  }

  closeMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  toggleTheme(): void {
    this.darkTheme.update(dark => !dark);
    this.document.documentElement.classList.toggle('app-dark', this.darkTheme());
  }
}
