export type PointSourceKind = 'homeAssistant' | 'mqtt' | 'httpJson';

export interface PointSourceSummary {
  id: string;
  name: string;
  description?: string;
  enabled: boolean;
  kind: PointSourceKind;
  revision: number;
  updatedAt: string;
}

export interface PointSourcePage {
  items: PointSourceSummary[];
  totalItems: number;
  page: number;
  pageSize: number;
  pageCount: number;
}

export interface ConnectionTestStage {
  name: string;
  status: 'passed' | 'failed';
  diagnostic?: string;
}

export interface ConnectionTestResult {
  status: 'passed' | 'failed';
  durationMilliseconds: number;
  stages: ConnectionTestStage[];
}

const messageFrom = async (response: Response): Promise<string> => {
  try {
    return (
      ((await response.json()) as { message?: string }).message ??
      `Request failed (${response.status})`
    );
  } catch {
    return `Request failed (${response.status})`;
  }
};

const request = async (url: string, init?: RequestInit): Promise<Response> => {
  const response = await fetch(url, init);
  if (!response.ok) throw new Error(await messageFrom(response));
  return response;
};

export const pointSourceApi = {
  async list(signal?: AbortSignal): Promise<PointSourcePage> {
    const response = await request('/api/point-sources?page=1&pageSize=50', { signal });
    return response.json() as Promise<PointSourcePage>;
  },
  async get(id: string, signal?: AbortSignal): Promise<{ yaml: string; revision: number }> {
    const response = await request(`/api/point-sources/${encodeURIComponent(id)}`, { signal });
    return {
      yaml: await response.text(),
      revision: Number(response.headers.get('ETag') ?? 0)
    };
  },
  async create(yaml: string): Promise<{ yaml: string; revision: number }> {
    const response = await request('/api/point-sources', {
      method: 'POST',
      headers: { 'Content-Type': 'application/yaml' },
      body: yaml
    });
    return { yaml: await response.text(), revision: Number(response.headers.get('ETag') ?? 0) };
  },
  async update(
    id: string,
    yaml: string,
    revision: number
  ): Promise<{ yaml: string; revision: number }> {
    const response = await request(`/api/point-sources/${encodeURIComponent(id)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/yaml', 'If-Match': String(revision) },
      body: yaml
    });
    return { yaml: await response.text(), revision: Number(response.headers.get('ETag') ?? 0) };
  },
  async delete(id: string, revision: number): Promise<void> {
    await request(`/api/point-sources/${encodeURIComponent(id)}?revision=${revision}`, {
      method: 'DELETE'
    });
  },
  async test(yaml: string, signal: AbortSignal): Promise<ConnectionTestResult> {
    const response = await request('/api/point-sources/test', {
      method: 'POST',
      headers: { 'Content-Type': 'application/yaml' },
      body: yaml,
      signal
    });
    return response.json() as Promise<ConnectionTestResult>;
  }
};
