import { expect, Locator, test } from '@playwright/test';
import { demoEnabled, demoUsers, login, openSeededBoard } from './support/demo';

test.describe('persisted application theme', () => {
  test.skip(!demoEnabled, 'Requires E2E_DEMO=true and the complete seeded stack.');

  test('dark theme keeps PrimeNG and application surfaces readable', async ({ page }) => {
    await login(page, demoUsers.owner.email);
    await page.getByRole('button', { name: 'Usar tema oscuro' }).click();

    await expect(page.locator('html')).toHaveClass(/app-dark/);
    await expect(page.locator('#app-theme')).toHaveAttribute('href', /lara-dark-blue\/theme\.css$/);
    await expectDarkSurface(page.locator('.topbar'));
    await expectDarkSurface(page.locator('.sidebar'));
    await expectDarkSurface(page.locator('.table-card'));
    await expectDarkSurface(page.locator('.p-datatable-thead th').first());
    await expectDarkSurface(page.locator('.p-datatable-tbody tr').first());

    await page.reload();
    await expect(page.locator('html')).toHaveClass(/app-dark/);
    await expect(page.getByRole('button', { name: 'Usar tema claro' })).toBeVisible();

    await page.getByRole('button', { name: 'Editar proyecto' }).first().click();
    const projectDialog = page.getByRole('dialog', { name: 'Editar proyecto' });
    await expectDarkSurface(projectDialog.locator('.p-dialog-header'));
    await expectDarkSurface(projectDialog.locator('.p-dialog-content'));
    await expectDarkSurface(projectDialog.locator('.p-inputtext').first());
    await page.keyboard.press('Escape');

    await openSeededBoard(page);
    await expectDarkSurface(page.locator('.filters'));
    await expectDarkSurface(page.locator('.board-column').first());
    await expectDarkSurface(page.locator('.task-card').first());

    await page.getByRole('button', { name: 'Editar tarea' }).first().click();
    const taskDialog = page.getByRole('dialog', { name: 'Editar tarea' });
    await expectDarkSurface(taskDialog.locator('.p-dialog-header'));
    await expectDarkSurface(taskDialog.locator('.p-dialog-content'));
    await expectDarkSurface(taskDialog.locator('.p-inputtext').first());
  });
});

async function expectDarkSurface(locator: Locator): Promise<void> {
  await expect(locator).toBeVisible();
  const metrics = await locator.evaluate(element => {
    const style = getComputedStyle(element);
    const parse = (value: string): [number, number, number] => {
      const channels = value.match(/[\d.]+/g)?.slice(0, 3).map(Number);
      if (!channels || channels.length !== 3) throw new Error(`Unsupported computed color: ${value}`);
      return channels as [number, number, number];
    };
    const luminance = ([red, green, blue]: [number, number, number]): number => {
      const channels = [red, green, blue].map(channel => {
        const normalized = channel / 255;
        return normalized <= .03928 ? normalized / 12.92 : ((normalized + .055) / 1.055) ** 2.4;
      });
      return .2126 * channels[0] + .7152 * channels[1] + .0722 * channels[2];
    };
    const background = luminance(parse(style.backgroundColor));
    const foreground = luminance(parse(style.color));
    return {
      background,
      contrast: (Math.max(background, foreground) + .05) / (Math.min(background, foreground) + .05)
    };
  });

  expect(metrics.background).toBeLessThan(.2);
  expect(metrics.contrast).toBeGreaterThanOrEqual(4.5);
}
