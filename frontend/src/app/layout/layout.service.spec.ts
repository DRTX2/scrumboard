import { TestBed } from '@angular/core/testing';
import { LayoutService } from './layout.service';

describe('LayoutService', () => {
  it('exposes only Proyectos in the Sakai menu and controls shell state', () => {
    const service = TestBed.inject(LayoutService);
    expect(service.menuItems).toEqual([{ label: 'Proyectos', icon: 'pi pi-th-large', routerLink: '/projects' }]);
    service.toggleMenu();
    expect(service.mobileMenuOpen()).toBeTrue();
    service.toggleTheme();
    expect(document.documentElement.classList.contains('app-dark')).toBeTrue();
    service.toggleTheme();
  });
});
