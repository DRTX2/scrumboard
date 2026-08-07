import { TestBed } from '@angular/core/testing';
import { LayoutService } from './layout.service';

describe('LayoutService', () => {
  let themeLink: HTMLLinkElement;

  beforeEach(() => {
    localStorage.removeItem('scrumboard.theme');
    document.documentElement.classList.remove('app-dark');
    document.getElementById('app-theme')?.remove();
    themeLink = document.createElement('link');
    themeLink.id = 'app-theme';
    themeLink.href = 'assets/themes/lara-light-blue/theme.css';
    document.head.append(themeLink);
  });

  afterEach(() => {
    localStorage.removeItem('scrumboard.theme');
    document.documentElement.classList.remove('app-dark');
    themeLink.remove();
    TestBed.resetTestingModule();
  });

  it('exposes only Proyectos in the Sakai menu and controls the mobile shell', () => {
    const service = TestBed.inject(LayoutService);
    expect(service.menuItems).toEqual([{ label: 'Proyectos', icon: 'pi pi-th-large', routerLink: '/projects' }]);
    service.toggleMenu();
    expect(service.mobileMenuOpen()).toBeTrue();
  });

  it('switches both custom and PrimeNG themes and persists the choice', () => {
    localStorage.setItem('scrumboard.theme', 'light');
    const service = TestBed.inject(LayoutService);
    service.initializeTheme();
    service.toggleTheme();
    expect(service.darkTheme()).toBeTrue();
    expect(document.documentElement.classList.contains('app-dark')).toBeTrue();
    expect(themeLink.getAttribute('href')).toBe('assets/themes/lara-dark-blue/theme.css');
    expect(localStorage.getItem('scrumboard.theme')).toBe('dark');

    service.toggleTheme();
    expect(service.darkTheme()).toBeFalse();
    expect(document.documentElement.classList.contains('app-dark')).toBeFalse();
    expect(themeLink.getAttribute('href')).toBe('assets/themes/lara-light-blue/theme.css');
    expect(localStorage.getItem('scrumboard.theme')).toBe('light');
  });

  it('restores a persisted dark preference during initialization', () => {
    localStorage.setItem('scrumboard.theme', 'dark');
    const service = TestBed.inject(LayoutService);
    service.initializeTheme();

    expect(service.darkTheme()).toBeTrue();
    expect(document.documentElement.classList.contains('app-dark')).toBeTrue();
    expect(themeLink.getAttribute('href')).toBe('assets/themes/lara-dark-blue/theme.css');
  });
});
