export interface YamlResource {
  yaml: string;
  revision: number;
}

export interface RuntimeEnvelope {
  pointId: string;
  value: unknown;
  units?: string;
  quality: string;
  reliability: string;
  sourceTimestamp?: string;
  updatedAt?: string;
  connectionState: string;
  status: 'live' | 'cached' | 'simulated' | 'unavailable';
  diagnostic: string;
  deviceResponse?: {
    statusCode: number;
    reasonPhrase?: string;
    contentType?: string;
    body: string;
  };
}

export interface ValidationDiagnostic {
  code: string;
  path: string;
  message: string;
  line?: number;
  column?: number;
}

interface YamlCrudApi {
  get: (id: string, signal?: AbortSignal) => Promise<YamlResource>;
  create: (yaml: string) => Promise<YamlResource>;
  update: (id: string, yaml: string, revision: number) => Promise<YamlResource>;
  delete: (id: string, revision: number) => Promise<void>;
}

export class YamlResourceError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly details?: unknown
  ) {
    super(message);
  }
}

const request = async (
  url: string,
  init?: RequestInit,
  options: { trackWait?: boolean } = {}
): Promise<Response> => {
  const response = await waitForFetch(url, init, options);
  if (response.ok) return response;
  let message = `Request failed (${response.status})`;
  let details: unknown;
  try {
    const body = (await response.json()) as { message?: unknown; details?: unknown };
    if (typeof body.message === 'string') message = body.message;
    details = body.details;
  } catch {
    // The response status is the fallback when the error body is not JSON.
  }
  throw new YamlResourceError(message, response.status, details);
};

const yamlResult = async (response: Response): Promise<YamlResource> => ({
  yaml: await response.text(),
  revision: Number(response.headers.get('ETag') ?? 0)
});

const yamlApi = (base: string): YamlCrudApi => ({
  async get(id: string, signal?: AbortSignal): Promise<YamlResource> {
    return yamlResult(await request(`${base}/${encodeURIComponent(id)}`, { signal }));
  },
  async create(yaml: string): Promise<YamlResource> {
    return yamlResult(
      await request(base, {
        method: 'POST',
        headers: { 'Content-Type': 'application/yaml' },
        body: yaml
      })
    );
  },
  async update(id: string, yaml: string, revision: number): Promise<YamlResource> {
    return yamlResult(
      await request(`${base}/${encodeURIComponent(id)}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/yaml',
          'If-Match': String(revision)
        },
        body: yaml
      })
    );
  },
  async delete(id: string, revision: number): Promise<void> {
    await request(`${base}/${encodeURIComponent(id)}?revision=${revision}`, {
      method: 'DELETE'
    });
  }
});

export const pointConfigurationApi = {
  ...yamlApi('/api/points'),
  async runtime(
    id: string,
    signal?: AbortSignal,
    options: { trackWait?: boolean } = {}
  ): Promise<RuntimeEnvelope> {
    const response = await request(
      `/api/points/${encodeURIComponent(id)}/runtime`,
      { signal },
      options
    );
    return response.json() as Promise<RuntimeEnvelope>;
  }
};

export const pointGroupConfigurationApi = {
  ...yamlApi('/api/point-groups'),
  async makeStandalone(id: string, revision: number): Promise<void> {
    await request(
      `/api/point-groups/${encodeURIComponent(id)}/make-points-standalone?revision=${revision}`,
      { method: 'POST' }
    );
  }
};

export const controllerTemplateConfigurationApi = {
  ...yamlApi('/api/controller-templates'),
  async get(id: string, signal?: AbortSignal): Promise<YamlResource> {
    return yamlResult(
      await request(`/api/controller-templates/${encodeURIComponent(id)}/yaml`, { signal })
    );
  },
  async create(yaml: string): Promise<YamlResource> {
    const response = await request('/api/controller-templates', {
      method: 'POST',
      headers: { 'Content-Type': 'application/yaml' },
      body: yaml
    });
    return { yaml, revision: Number(response.headers.get('ETag') ?? 0) };
  },
  async update(id: string, yaml: string, revision: number): Promise<YamlResource> {
    const response = await request(`/api/controller-templates/${encodeURIComponent(id)}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/yaml',
        'If-Match': String(revision)
      },
      body: yaml
    });
    return { yaml, revision: Number(response.headers.get('ETag') ?? 0) };
  },
  async validate(yaml: string): Promise<ValidationDiagnostic[]> {
    const response = await request('/api/controller-templates/validate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/yaml' },
      body: yaml
    });
    const body = (await response.json()) as {
      valid?: boolean;
      diagnostics?: ValidationDiagnostic[];
    };
    return body.diagnostics ?? [];
  }
};
import { waitForFetch } from '@/api/waitForFetch';
