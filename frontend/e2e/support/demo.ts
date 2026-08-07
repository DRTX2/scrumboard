import { randomUUID } from 'node:crypto';
import { expect, Page, test as base } from '@playwright/test';

export const demoEnabled = process.env['E2E_DEMO'] === 'true';
export const demoPassword = 'ScrumBoard123!';

export const demoUsers = {
  owner: { email: 'owner@scrumboard.local', name: 'Demo Owner' },
  member: { email: 'member@scrumboard.local', name: 'Demo Member' }
} as const;

interface CreatedTask {
  apiBaseUrl: string;
  projectId: string;
  id: string;
  title: string;
  etag: string;
}

interface TaskMutationResponse {
  id: string;
  etag?: string;
}

interface BoardResponse {
  data?: BoardResponse;
  columns?: Array<{ tasks?: Array<{ id: string; etag?: string }> }>;
}

interface CollaborationFixture {
  ownerPage: Page;
  memberPage: Page;
  projectId: string;
  registerTask: (task: CreatedTask) => void;
}

export async function login(page: Page, email: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Correo electrónico').fill(email);
  await page.getByLabel('Contraseña').fill(demoPassword);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page).toHaveURL(/\/projects(?:[?#].*)?$/);
  await expect(page.getByRole('heading', { name: 'Proyectos', exact: true })).toBeVisible();
}

export async function openSeededBoard(page: Page): Promise<string> {
  await page.getByRole('button', { name: /ScrumBoard Launch/ }).click();
  await expect(page.getByRole('heading', { name: 'ScrumBoard Launch', exact: true })).toBeVisible();

  const match = new URL(page.url()).pathname.match(/^\/projects\/([^/]+)\/board$/);
  if (!match?.[1]) throw new Error(`Unexpected seeded board URL: ${page.url()}`);
  return decodeURIComponent(match[1]);
}

export async function loginAndOpenSeededBoard(page: Page, email: string): Promise<string> {
  await login(page, email);
  return openSeededBoard(page);
}

export function uniqueName(prefix: string, workerIndex: number, retry: number): string {
  return `${prefix} ${workerIndex}-${retry}-${Date.now()}-${randomUUID().slice(0, 8)}`;
}

export async function accessToken(page: Page): Promise<string> {
  const token = await page.evaluate(() => {
    const session = JSON.parse(sessionStorage.getItem('scrumboard.session') ?? 'null') as { token?: unknown } | null;
    return typeof session?.token === 'string' ? session.token : null;
  });
  if (!token) throw new Error('The authenticated browser context has no access token.');
  return token;
}

export async function apiBaseUrl(page: Page): Promise<string> {
  return page.evaluate(async () => {
    const response = await fetch('/assets/app-config.json', { cache: 'no-store' });
    if (!response.ok) throw new Error(`Could not load runtime configuration (${response.status}).`);
    const config = await response.json() as { apiBaseUrl: string };
    return new URL(config.apiBaseUrl.replace(/\/$/, ''), window.location.origin).href.replace(/\/$/, '');
  });
}

export async function bearerHeaders(page: Page): Promise<Record<string, string>> {
  return { Authorization: `Bearer ${await accessToken(page)}`, Accept: 'application/json' };
}

async function currentTaskEtag(page: Page, task: CreatedTask): Promise<{ found: boolean; etag?: string }> {
  const response = await page.request.get(
    `${task.apiBaseUrl}/v1/projects/${encodeURIComponent(task.projectId)}/board`,
    {
      headers: await bearerHeaders(page),
      params: { search: task.title, taskLimit: 50 }
    }
  );
  if (!response.ok()) throw new Error(`Could not read the board during cleanup (${response.status()}).`);

  const body = await response.json() as BoardResponse;
  const board = body.data ?? body;
  const current = board.columns?.flatMap(column => column.tasks ?? []).find(item => item.id === task.id);
  return current ? { found: true, etag: current.etag } : { found: false };
}

async function deleteCreatedTask(page: Page, task: CreatedTask): Promise<void> {
  const current = await currentTaskEtag(page, task);
  if (!current.found) return;

  const response = await page.request.delete(
    `${task.apiBaseUrl}/v1/projects/${encodeURIComponent(task.projectId)}/tasks/${encodeURIComponent(task.id)}`,
    { headers: { ...await bearerHeaders(page), 'If-Match': current.etag ?? task.etag } }
  );
  if (response.status() !== 204 && response.status() !== 404) {
    throw new Error(`Could not delete E2E task ${task.id} (${response.status()}): ${await response.text()}`);
  }
}

export const collaborationTest = base.extend<{ collaboration: CollaborationFixture }>({
  collaboration: async ({ browser, baseURL }, use) => {
    if (!baseURL) throw new Error('Playwright baseURL is required by the collaboration fixture.');

    const [ownerContext, memberContext] = await Promise.all([
      browser.newContext({ baseURL }),
      browser.newContext({ baseURL })
    ]);
    const ownerPage = await ownerContext.newPage();
    const memberPage = await memberContext.newPage();
    const createdTasks: CreatedTask[] = [];

    try {
      const [ownerProjectId, memberProjectId] = await Promise.all([
        loginAndOpenSeededBoard(ownerPage, demoUsers.owner.email),
        loginAndOpenSeededBoard(memberPage, demoUsers.member.email)
      ]);
      expect(memberProjectId).toBe(ownerProjectId);

      await Promise.all([
        expect(ownerPage.getByText('2 en línea', { exact: true })).toBeVisible({ timeout: 15000 }),
        expect(memberPage.getByText('2 en línea', { exact: true })).toBeVisible({ timeout: 15000 })
      ]);

      await use({
        ownerPage,
        memberPage,
        projectId: ownerProjectId,
        registerTask: task => createdTasks.push(task)
      });
    } finally {
      try {
        for (const task of createdTasks.reverse()) await deleteCreatedTask(ownerPage, task);
      } finally {
        await Promise.all([ownerContext.close(), memberContext.close()]);
      }
    }
  }
});

export async function createTaskAssignedToMember(
  fixture: CollaborationFixture,
  title: string
): Promise<TaskMutationResponse> {
  const { ownerPage, projectId, registerTask } = fixture;
  const backlog = ownerPage.getByRole('article').filter({
    has: ownerPage.getByRole('heading', { name: 'Backlog', exact: true })
  }).first();

  await backlog.getByRole('button', { name: 'Agregar tarea' }).click();
  const dialog = ownerPage.getByRole('dialog', { name: 'Nueva tarea' });
  await expect(dialog).toBeVisible();
  await dialog.getByLabel('Título').fill(title);
  await dialog.getByLabel('Descripción').fill('Created by Playwright to verify real-time collaboration.');
  await dialog.getByRole('combobox', { name: 'Responsable' }).click();
  await ownerPage.getByRole('option', { name: demoUsers.member.name, exact: true }).click();

  const responsePromise = ownerPage.waitForResponse(response =>
    response.request().method() === 'POST' && /\/api\/v1\/projects\/[^/]+\/tasks$/.test(new URL(response.url()).pathname)
  );
  await dialog.getByRole('button', { name: 'Guardar' }).click();
  const response = await responsePromise;
  expect(response.status()).toBe(201);

  const body = await response.json() as TaskMutationResponse;
  const etag = response.headers()['etag'] ?? body.etag;
  if (!body.id || !etag) throw new Error('Task creation did not return an id and ETag.');

  registerTask({ apiBaseUrl: await apiBaseUrl(ownerPage), projectId, id: body.id, title, etag });
  return body;
}
