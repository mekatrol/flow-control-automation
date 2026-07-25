import type { Page, Route } from '@playwright/test';

import pointsDocument from '@contracts/points/v1.normalized.json';
import sourcesDocument from '@contracts/point-sources/v1.normalized.json';

interface PageResponse {
  items: unknown[];
  totalItems: number;
  page: number;
  pageSize: number;
  pageCount: number;
}

const collectionPage = (items: unknown[]): PageResponse => ({
  items: structuredClone(items),
  totalItems: items.length,
  page: 1,
  pageSize: Math.max(10, items.length),
  pageCount: 1
});

const fulfillCollection = async (route: Route, items: unknown[]): Promise<void> => {
  await route.fulfill({ json: collectionPage(items) });
};

/**
 * Seed the planned point API routes with the canonical Phase 0 fixtures.
 * Specs can override individual routes after this helper returns.
 */
export const seedPointApi = async (page: Page): Promise<void> => {
  await page.route(/\/api\/points(?:\?.*)?$/, (route) =>
    fulfillCollection(route, pointsDocument.points)
  );
  await page.route(/\/api\/point-groups(?:\?.*)?$/, (route) =>
    fulfillCollection(route, pointsDocument.groups)
  );
  await page.route(/\/api\/point-sources(?:\?.*)?$/, (route) =>
    fulfillCollection(route, sourcesDocument.sources)
  );
};

