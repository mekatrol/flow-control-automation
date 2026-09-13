import { waitForFetch } from '@/api/waitForFetch';

export type ConfigurationGuidanceType = 'point' | 'point-source' | 'controller-template';

export const fetchConfigurationGuidance = async (
  type: ConfigurationGuidanceType,
  yaml: string,
  pointId?: string
): Promise<string> => {
  const query = pointId ? `?pointId=${encodeURIComponent(pointId)}` : '';
  const response = await waitForFetch(
    `/api/configuration-guidance/${type}${query}`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/yaml', Accept: 'text/markdown' },
      body: yaml
    },
    { trackWait: false }
  );
  if (response.ok) return response.text();

  let message = `Unable to load guidance (${response.status})`;
  try {
    const body = (await response.json()) as { message?: unknown };
    if (typeof body.message === 'string') message = body.message;
  } catch {
    // The response status is a useful fallback when the body is not JSON.
  }
  throw new Error(message);
};
