import { expect } from '@playwright/test';
import {
  collaborationTest as test,
  createTaskAssignedToMember,
  demoEnabled,
  uniqueName
} from './support/demo';

test.describe('seeded real-time collaboration', () => {
  test.skip(!demoEnabled, 'Requires E2E_DEMO=true and the complete seeded stack.');

  test('owner creates a member task that arrives through SignalR', async ({ collaboration }, testInfo) => {
    const { ownerPage, memberPage } = collaboration;
    expect(ownerPage.context()).not.toBe(memberPage.context());

    const title = uniqueName('E2E realtime task', testInfo.workerIndex, testInfo.retry);
    const memberBoardUrl = memberPage.url();
    await expect(memberPage.getByRole('heading', { name: title, exact: true })).toHaveCount(0);

    const task = await createTaskAssignedToMember(collaboration, title);

    await expect(memberPage.getByRole('heading', { name: title, exact: true })).toBeVisible({ timeout: 2000 });
    await expect(memberPage).toHaveURL(memberBoardUrl);

    const ownerBacklog = column(ownerPage, 'Backlog');
    const ownerProgress = column(ownerPage, 'In progress');
    const memberProgress = column(memberPage, 'In progress');
    const moveResponse = ownerPage.waitForResponse(response =>
      response.request().method() === 'PATCH' &&
      new URL(response.url()).pathname.endsWith(`/tasks/${task.id}`)
    );

    await dragWithMouse(
      ownerPage,
      ownerBacklog.locator('.task-card').filter({ hasText: title }),
      ownerProgress.locator('.task-list')
    );
    expect((await moveResponse).ok()).toBe(true);
    const acceptedAt = Date.now();
    await expect(memberProgress.getByRole('heading', { name: title, exact: true })).toBeVisible({ timeout: 2000 });
    expect(Date.now() - acceptedAt).toBeLessThan(2000);
  });
});

function column(page: import('@playwright/test').Page, name: string) {
  return page.locator('.board-column').filter({
    has: page.getByRole('heading', { name, exact: true })
  });
}

async function dragWithMouse(
  page: import('@playwright/test').Page,
  source: import('@playwright/test').Locator,
  target: import('@playwright/test').Locator
): Promise<void> {
  const sourceBox = await source.boundingBox();
  const targetBox = await target.boundingBox();
  if (!sourceBox || !targetBox) throw new Error('Task drag source or target is not visible.');

  const sourcePoint = { x: sourceBox.x + sourceBox.width / 2, y: sourceBox.y + sourceBox.height / 2 };
  const targetPoint = { x: targetBox.x + targetBox.width / 2, y: targetBox.y + targetBox.height - 24 };
  await page.mouse.move(sourcePoint.x, sourcePoint.y);
  await page.mouse.down();
  await page.mouse.move(sourcePoint.x + 12, sourcePoint.y + 12, { steps: 3 });
  await page.mouse.move(targetPoint.x, targetPoint.y, { steps: 15 });
  await page.waitForTimeout(150);
  await page.mouse.up();
}
