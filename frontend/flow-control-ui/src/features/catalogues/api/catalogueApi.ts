import {
  parseControllerTemplateList,
  parsePage,
  parsePoint,
  parsePointGroup,
  type ControllerTemplateSummary,
  type Page,
  type PointGroupSummary,
  type PointSummary
} from './catalogueDto';

export class CatalogueApiError extends Error {
  constructor(
    message: string,
    readonly status: number
  ) {
    super(message);
  }
}

export interface CatalogueQuery {
  filter: string;
  page: number;
  pageSize: number;
  sort?: 'ascending' | 'descending';
}

const queryString = (query: CatalogueQuery): string => {
  const parameters = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
    sort: query.sort ?? 'ascending'
  });
  if (query.filter.trim()) parameters.set('filter', query.filter.trim());
  return parameters.toString();
};

const getJson = async (url: string, signal?: AbortSignal): Promise<unknown> => {
  const response = await waitForFetch(url, { signal });
  if (!response.ok) {
    let message = `Request failed (${response.status})`;
    try {
      const body = (await response.json()) as { message?: unknown };
      if (typeof body.message === 'string') message = body.message;
    } catch {
      // The response status is the fallback when the error body is not JSON.
    }
    throw new CatalogueApiError(message, response.status);
  }
  return response.json() as Promise<unknown>;
};

export const catalogueApi = {
  async points(query: CatalogueQuery, signal?: AbortSignal): Promise<Page<PointSummary>> {
    return parsePage(await getJson(`/api/points?${queryString(query)}`, signal), parsePoint);
  },
  async groups(query: CatalogueQuery, signal?: AbortSignal): Promise<Page<PointGroupSummary>> {
    return parsePage(
      await getJson(`/api/point-groups?${queryString(query)}`, signal),
      parsePointGroup
    );
  },
  async controllerTemplates(signal?: AbortSignal): Promise<ControllerTemplateSummary[]> {
    return parseControllerTemplateList(await getJson('/api/controller-templates', signal));
  }
};
import { waitForFetch } from '@/api/waitForFetch';
