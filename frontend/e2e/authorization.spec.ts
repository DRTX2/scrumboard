import { expect, test } from '@playwright/test';
import {
  apiBaseUrl,
  bearerHeaders,
  demoEnabled,
  demoUsers,
  loginAndOpenSeededBoard,
  uniqueName
} from './support/demo';

test.describe('seeded role authorization', () => {
  test.skip(!demoEnabled, 'Requires E2E_DEMO=true and the complete seeded stack.');

  test('member direct column mutation is rejected by the API', async ({ page }, testInfo) => {
    const projectId = await loginAndOpenSeededBoard(page, demoUsers.member.email);
    const response = await page.request.post(
      `${await apiBaseUrl(page)}/v1/projects/${encodeURIComponent(projectId)}/columns`,
      {
        headers: {
          ...await bearerHeaders(page),
          'Idempotency-Key': randomIdempotencyKey(testInfo.workerIndex, testInfo.retry)
        },
        data: { name: uniqueName('Forbidden E2E column', testInfo.workerIndex, testInfo.retry) }
      }
    );

    expect(response.status()).toBe(403);
  });

  test('member cannot see owner-only column administration', async ({ page }) => {
    await loginAndOpenSeededBoard(page, demoUsers.member.email);
    await expect(page.getByRole('button', { name: 'Nueva columna' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Editar columna', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Eliminar columna', exact: true })).toHaveCount(0);
  });
});

function randomIdempotencyKey(workerIndex: number, retry: number): string {
  return `e2e-member-column-${workerIndex}-${retry}-${Date.now()}`;
}
