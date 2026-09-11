import { waitForFetch } from '@/api/waitForFetch';
import {
  parseControllerTemplateList,
  type ControllerTemplateSummary
} from './controllerTemplateDto';

export class ControllerTemplateApiError extends Error {
  constructor(
    message: string,
    readonly status: number
  ) {
    super(message);
  }
}

export const controllerTemplateApi = {
  async list(signal?: AbortSignal): Promise<ControllerTemplateSummary[]> {
    const response = await waitForFetch('/api/controller-templates', { signal });
    if (!response.ok) {
      let message = `Request failed (${response.status})`;
      try {
        const body = (await response.json()) as { message?: unknown };
        if (typeof body.message === 'string') message = body.message;
      } catch {
        // The response status is the fallback when the error body is not JSON.
      }
      throw new ControllerTemplateApiError(message, response.status);
    }
    return parseControllerTemplateList(await response.json());
  }
};
