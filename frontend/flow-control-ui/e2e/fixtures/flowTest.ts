import { expect, test as base, type Page } from '@playwright/test';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import type { FlowDefinition } from '@/features/flows/types';

export { expect };

export const flowsCollectionPattern = /\/api\/flows(?:\?.*)?$/;

export const pagedFlows = (flows: FlowDefinition[], requestUrl: string): {
  items: FlowDefinition[];
  totalItems: number;
  page: number;
  pageSize: number;
  pageCount: number;
} => {
  const query = new URL(requestUrl).searchParams;
  const filter = (query.get('filter') ?? '').toLocaleLowerCase();
  const statuses = query.getAll('status');
  const pageSize = Number(query.get('pageSize') ?? 10);
  const direction = query.get('sort') === 'descending' ? -1 : 1;
  const matches = flows
    .filter(
      (flow) =>
        flow.name.toLocaleLowerCase().includes(filter) &&
        (statuses.length === 0 || statuses.includes(flow.status))
    )
    .sort((left, right) => direction * left.name.localeCompare(right.name));
  const pageCount = Math.max(1, Math.ceil(matches.length / pageSize));
  const page = Math.min(Number(query.get('page') ?? 1), pageCount);
  const start = (page - 1) * pageSize;
  return {
    items: matches.slice(start, start + pageSize),
    totalItems: matches.length,
    page,
    pageSize,
    pageCount
  };
};

/**
 * Extend Playwright with an automatic API fixture instead of an imported hook.
 * Automatic fixtures are applied to every spec that imports this `test`, even
 * when Playwright loads those spec modules independently and in parallel.
 */
export const test = base.extend<{ mockFlowsApi: void }>({
  mockFlowsApi: [
    async ({ page }, use) => {
      await page.route(flowsCollectionPattern, async (route) => {
        if (route.request().method() === 'POST') {
          const { name } = route.request().postDataJSON() as { name: string };
          const id = name
            .toLocaleLowerCase()
            .replaceAll(/[^a-z0-9]+/g, '-')
            .replaceAll(/^-|-$/g, '');
          await route.fulfill({
            json: {
              id,
              name,
              description: '',
              status: 'draft',
              updatedAt: '2026-07-13T12:00:00+10:00',
              nodes: [],
              connections: []
            }
          });
          return;
        }
        await route.fulfill({ json: pagedFlows(sampleFlows, route.request().url()) });
      });
      await page.route('**/api/flows/*', async (route) => {
        const flowId = decodeURIComponent(
          new URL(route.request().url()).pathname.split('/').at(-1) ?? ''
        );
        const flow = sampleFlows.find(({ id }) => id === flowId);
        if (!flow) {
          await route.fulfill({ status: 404, json: { message: 'not found' } });
          return;
        }
        if (route.request().method() === 'DELETE') {
          await route.fulfill({ status: 204 });
          return;
        }
        if (route.request().method() === 'PUT') {
          await route.fulfill({ json: route.request().postDataJSON() });
          return;
        }
        await route.fulfill({ json: flow });
      });
      await page.route('**/api/flows/*/runtime', async (route) => {
        const flowId = decodeURIComponent(
          new URL(route.request().url()).pathname.split('/').at(-2) ?? ''
        );
        await route.fulfill({
          json: {
            flowId,
            state: 'stopped',
            updatedAt: '2026-07-14T08:00:00+10:00',
            nodes: {}
          }
        });
      });

      // Yield only after all routes are installed. The test cannot navigate
      // early and accidentally leak a request to the development proxy.
      await use();
    },
    { auto: true }
  ]
});

/**
 * Replace the default read-only flow routes with a small stateful API.
 *
 * CRUD tests intentionally get a fresh copy of this state for every test. That
 * keeps each user operation independently repeatable and prevents a failure in
 * one operation from hiding a failure in a later operation.
 */
export const useMutableFlowsApi = async (page: Page): Promise<void> => {
  await page.unroute(flowsCollectionPattern);
  await page.unroute('**/api/flows/*');
  let serverFlows = structuredClone(sampleFlows);

  // Model the collection endpoint closely enough to verify that the library
  // renders server state after a create instead of merely updating local UI.
  await page.route(flowsCollectionPattern, async (route) => {
    if (route.request().method() === 'POST') {
      const { name } = route.request().postDataJSON() as { name: string };
      const created = {
        id: 'new-automation',
        name,
        description: '',
        status: 'draft' as const,
        updatedAt: '2026-07-13T12:00:00+10:00',
        nodes: [],
        connections: []
      };
      serverFlows.push(created);
      await route.fulfill({ json: created });
      return;
    }
    await route.fulfill({ json: pagedFlows(serverFlows, route.request().url()) });
  });

  // Model item reads and mutations against the same in-memory collection. Each
  // response therefore represents what a real persistence API would return.
  await page.route('**/api/flows/*', async (route) => {
    const id = new URL(route.request().url()).pathname.split('/').at(-1);
    const index = serverFlows.findIndex((flow) => flow.id === id);
    if (index < 0) {
      await route.fulfill({ status: 404 });
      return;
    }
    if (route.request().method() === 'PUT') {
      serverFlows[index] = route.request().postDataJSON();
      await route.fulfill({ json: serverFlows[index] });
      return;
    }
    if (route.request().method() === 'DELETE') {
      serverFlows = serverFlows.filter((flow) => flow.id !== id);
      await route.fulfill({ status: 204 });
      return;
    }
    await route.fulfill({ json: serverFlows[index] });
  });
};
