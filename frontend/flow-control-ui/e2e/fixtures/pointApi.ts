import type { Page, Route } from '@playwright/test';

import homeAssistant from '@contracts/point-sources/valid/home-assistant.v1.normalized.json';
import httpJson from '@contracts/point-sources/valid/http-json.v1.normalized.json';
import mqtt from '@contracts/point-sources/valid/mqtt.v1.normalized.json';
import physical from '@contracts/point-sources/valid/physical.v1.normalized.json';
import virtual from '@contracts/point-sources/valid/virtual.v1.normalized.json';

const sources = [homeAssistant, httpJson, mqtt, physical, virtual];
const points = sources.flatMap((source) =>
  source.points.map((point) => ({ ...point, sourceKind: source.kind, revision: 1 }))
);

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
    fulfillCollection(route, points)
  );
  await page.route(/\/api\/point-sources(?:\?.*)?$/, (route) =>
    fulfillCollection(route, sources)
  );
};
