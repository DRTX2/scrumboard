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

    await createTaskAssignedToMember(collaboration, title);

    await expect(memberPage.getByRole('heading', { name: title, exact: true })).toBeVisible({ timeout: 15000 });
    await expect(memberPage).toHaveURL(memberBoardUrl);
  });
});
