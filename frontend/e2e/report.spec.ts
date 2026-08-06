import { expect, test } from '@playwright/test';
import { demoEnabled, demoUsers, loginAndOpenSeededBoard } from './support/demo';

test.describe('seeded report download', () => {
  test.skip(!demoEnabled, 'Requires E2E_DEMO=true and the complete seeded stack.');

  test('PDF download is safe and preserves the visible board filter', async ({ page }) => {
    await loginAndOpenSeededBoard(page, demoUsers.owner.email);
    const search = 'Review product backlog';
    const searchInput = page.getByPlaceholder('Buscar tareas');

    await searchInput.fill(search);
    await expect(page.getByRole('heading', { name: search, exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Build collaborative board', exact: true })).toHaveCount(0);

    const reportResponse = page.waitForResponse(response => {
      const url = new URL(response.url());
      return response.request().method() === 'GET' &&
        /\/api\/v1\/projects\/[^/]+\/reports$/.test(url.pathname) &&
        url.searchParams.get('format') === 'pdf' &&
        url.searchParams.get('search') === search;
    });
    const downloadEvent = page.waitForEvent('download');
    const reportButton = page.getByRole('button', { name: 'Descargar reporte PDF' }).or(
      page.getByRole('button').filter({ has: page.locator('.pi-file-pdf') })
    );
    await reportButton.click();
    const [response, download] = await Promise.all([reportResponse, downloadEvent]);

    expect(response.ok()).toBe(true);
    expect(response.headers()['content-type']?.split(';')[0]).toBe('application/pdf');
    expect(download.suggestedFilename()).toMatch(
      /^(?:ScrumBoard-Launch-\d{8}-\d{4}|scrumboard-launch-(?:reporte|report))\.pdf$/
    );
    expect(download.suggestedFilename()).not.toMatch(/[\\/\u0000-\u001f\u007f]/);
    expect((await response.body()).subarray(0, 5).toString('ascii')).toBe('%PDF-');
    expect(await download.failure()).toBeNull();

    await expect(searchInput).toHaveValue(search);
    await expect(page.getByRole('heading', { name: search, exact: true })).toBeVisible();
    await expect(page.getByText('Limpia los filtros para reordenar')).toBeVisible();
  });
});
