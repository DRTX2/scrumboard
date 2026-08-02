import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./core/auth/login.component').then(m => m.LoginComponent) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/app-layout.component').then(m => m.AppLayoutComponent),
    children: [
      { path: 'projects', loadComponent: () => import('./features/projects/projects.component').then(m => m.ProjectsComponent) },
      { path: 'projects/:id/board', loadComponent: () => import('./features/board/board.component').then(m => m.BoardComponent) },
      { path: '', pathMatch: 'full', redirectTo: 'projects' }
    ]
  },
  { path: '**', redirectTo: '' }
];
