import { expect, test } from '@playwright/test';

test('shows the responsive login entry point', async ({ page }) => {
  await page.goto('/login');
  await expect(page).toHaveTitle('ScrumBoard');
  await expect(page.getByRole('heading', { name: 'Bienvenido' })).toBeVisible();
  await expect(page.getByLabel('Correo electrónico')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();
});

test('authenticates and opens the seeded board', async ({ page }) => {
  test.skip(process.env['E2E_DEMO'] !== 'true', 'Requires the complete demo stack.');
  await page.goto('/login');
  await page.getByLabel('Correo electrónico').fill('owner@scrumboard.local');
  await page.getByLabel('Contraseña').fill('ScrumBoard123!');
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page).toHaveURL(/\/projects$/);
  await page.getByRole('button', { name: /ScrumBoard Launch/ }).click();
  await expect(page.getByRole('heading', { name: 'ScrumBoard Launch' })).toBeVisible();
  await expect(page.getByText('Review product backlog')).toBeVisible();
});
