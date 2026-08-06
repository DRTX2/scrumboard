import { Component, DestroyRef, ElementRef, HostListener, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AvatarModule } from 'primeng/avatar';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../core/auth/auth.service';
import { LayoutService } from './layout.service';

@Component({
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, AvatarModule, ButtonModule],
  templateUrl: './app-layout.component.html',
  styleUrl: './app-layout.component.scss'
})
export class AppLayoutComponent {
  private readonly destroyRef = inject(DestroyRef);
  @ViewChild('menuButton') private menuButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('sidebar') private sidebar?: ElementRef<HTMLElement>;
  readonly user = this.auth.user;
  readonly menuOpen = this.layout.mobileMenuOpen;
  readonly darkTheme = this.layout.darkTheme;
  readonly menuItems = this.layout.menuItems;

  constructor(private readonly auth: AuthService, readonly layout: LayoutService, router: Router) {
    router.events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.layout.closeMenu());
  }

  @HostListener('document:keydown.escape')
  closeMenu(): void {
    if (!this.menuOpen()) return;
    this.layout.closeMenu();
    this.menuButton?.nativeElement.focus();
  }

  toggleMenu(): void {
    if (this.menuOpen()) {
      this.closeMenu();
      return;
    }
    this.layout.toggleMenu();
    setTimeout(() => this.sidebar?.nativeElement.querySelector<HTMLElement>('a')?.focus());
  }

  logout(): void { this.auth.logout(); }
}
