export type CredentialKind = 'mqtt' | 'token';

export interface CredentialMetadata {
  id: string;
  name: string;
  kind: CredentialKind;
  username?: string;
  revision: number;
  createdAt: string;
  updatedAt: string;
}

export interface CredentialInput {
  id: string;
  name: string;
  kind: CredentialKind;
  username?: string;
  password?: string;
  token?: string;
  revision?: number;
}

const request = async (url: string, init?: RequestInit): Promise<Response> => {
  const response = await waitForFetch(url, init);
  if (!response.ok) {
    const body = (await response.json().catch(() => ({}))) as { message?: string };
    throw new Error(body.message ?? `Request failed (${response.status})`);
  }
  return response;
};

export const credentialApi = {
  async list(signal?: AbortSignal): Promise<CredentialMetadata[]> {
    const response = await request('/api/credentials', { signal });
    return ((await response.json()) as { items: CredentialMetadata[] }).items;
  },
  async create(input: CredentialInput): Promise<CredentialMetadata> {
    const response = await request('/api/credentials', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input)
    });
    return response.json() as Promise<CredentialMetadata>;
  },
  async update(input: CredentialInput): Promise<CredentialMetadata> {
    const response = await request(`/api/credentials/${encodeURIComponent(input.id)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input)
    });
    return response.json() as Promise<CredentialMetadata>;
  },
  async delete(id: string, revision: number): Promise<void> {
    await request(`/api/credentials/${encodeURIComponent(id)}?revision=${revision}`, {
      method: 'DELETE'
    });
  }
};
import { waitForFetch } from '@/api/waitForFetch';
