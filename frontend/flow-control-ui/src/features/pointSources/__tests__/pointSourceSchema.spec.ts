import { describe, expect, it } from 'vitest';

import { pointSourceSchema } from '@/features/pointSources/pointSourceSchema';

describe('pointSourceSchema', () => {
  it('accepts HTTP and HTTPS base URLs', () => {
    const sources = pointSourceSchema.properties?.sources;
    const source =
      typeof sources === 'object' && !Array.isArray(sources) ? sources.items : undefined;
    const connection =
      typeof source === 'object' && !Array.isArray(source)
        ? source.properties?.connection
        : undefined;
    const baseUrl =
      typeof connection === 'object' && !Array.isArray(connection)
        ? connection.properties?.baseUrl
        : undefined;
    const pattern =
      typeof baseUrl === 'object' && !Array.isArray(baseUrl) ? baseUrl.pattern : undefined;

    expect(pattern).toBeDefined();
    expect(new RegExp(pattern!)).toMatchObject(expect.any(RegExp));
    expect('http://lego-train.lan').toMatch(new RegExp(pattern!));
    expect('https://lego-train.lan').toMatch(new RegExp(pattern!));
    expect('ftp://lego-train.lan').not.toMatch(new RegExp(pattern!));
  });
});
