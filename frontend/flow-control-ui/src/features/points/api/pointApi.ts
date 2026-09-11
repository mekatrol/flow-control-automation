import { waitForFetch } from '@/api/waitForFetch';
import { parsePage, parsePoint, type Page, type PointSummary } from './pointDto';

export class PointApiError extends Error {
  constructor(
    message: string,
    readonly status: number
  ) {
    super(message);
  }
}

export interface PointQuery {
  filter: string;
  page: number;
  pageSize: number;
  sort?: 'ascending' | 'descending';
}

const queryString = (query: PointQuery): string => {
  const parameters = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
    sort: query.sort ?? 'ascending'
  });
  if (query.filter.trim()) parameters.set('filter', query.filter.trim());
  return parameters.toString();
};

export const pointApi = {
  async list(query: PointQuery, signal?: AbortSignal): Promise<Page<PointSummary>> {
    const response = await waitForFetch(`/api/points?${queryString(query)}`, { signal });
    if (!response.ok) {
      let message = `Request failed (${response.status})`;
      try {
        const body = (await response.json()) as { message?: unknown };
        if (typeof body.message === 'string') message = body.message;
      } catch {
        // The response status is the fallback when the error body is not JSON.
      }
      throw new PointApiError(message, response.status);
    }
    return parsePage(await response.json(), parsePoint);
  }
};
