import { expect, test } from '@playwright/test';
import { demoEnabled, demoUsers, loginAndOpenSeededBoard } from './support/demo';

test.describe('320px mobile smoke', () => {
  test.use({ viewport: { width: 320, height: 720 }, hasTouch: true, isMobile: true });

  test('login remains usable without body-level horizontal overflow', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: 'Bienvenido' })).toBeVisible();
    await expect(page.getByLabel('Correo electrónico')).toBeVisible();
    await expect(page.getByLabel('Contraseña')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();
    await expectNoBodyOverflow(page);
  });

  test('authenticated shell, sidebar, and board keep overflow contained', async ({ page }) => {
    test.skip(!demoEnabled, 'Requires E2E_DEMO=true and the complete seeded stack.');
    await loginAndOpenSeededBoard(page, demoUsers.owner.email);

    const menuButton = page.getByRole('button', { name: 'Abrir menú' });
    await expect(menuButton).toBeVisible();
    await menuButton.click();
    const sidebar = page.locator('aside');
    await expect.poll(() => sidebar.evaluate(element => element.getBoundingClientRect().left))
      .toBeGreaterThanOrEqual(0);
    await expect(sidebar.getByText('Proyectos', { exact: true })).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('button', { name: 'Abrir menú' })).toBeVisible();

    await expectNoBodyOverflow(page);
    await expect(page.getByRole('article').filter({
      has: page.getByRole('heading', { name: 'Backlog', exact: true })
    }).first()).toBeVisible();
    await expect.poll(() => page.locator('.columns').evaluate(element =>
      element.scrollWidth - element.clientWidth
    )).toBeGreaterThan(0);
  });
});

async function expectNoBodyOverflow(page: import('@playwright/test').Page): Promise<void> {
  await expect.poll(() => page.evaluate(() =>
    Math.max(document.documentElement.scrollWidth, document.body.scrollWidth) - document.documentElement.clientWidth
  )).toBeLessThanOrEqual(1);
}
