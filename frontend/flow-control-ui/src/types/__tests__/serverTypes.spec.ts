import { describe, expect, it } from 'vitest';
import * as serverTypes from '@/types/serverTypes';

const backendEnums = import.meta.glob<string>(
  '../../../../../backend/Server/Server.Common/Types/*.cs',
  { query: '?raw', import: 'default', eager: true }
);

describe('Server.Common JSON enum parity', () => {
  for (const [name, values] of Object.entries(serverTypes)) {
    if (typeof values !== 'object') continue;

    it(`${name} matches the backend members and serialized names`, () => {
      const source = backendEnums[`../../../../../backend/Server/Server.Common/Types/${name}.cs`];
      if (!source) throw new Error(`Missing backend enum ${name}`);
      const body = source.slice(source.indexOf('{') + 1, source.lastIndexOf('}'));
      const declarations = body.replace(/\/\/[^\n]*/g, '').split(',');
      const expected = Object.fromEntries(
        declarations
          .filter((item) => item.trim())
          .map((declaration) => {
            const explicitName = declaration.match(/JsonStringEnumMemberName\("([^"]+)"\)/)?.[1];
            const member = declaration
              .replace(/\[[^\]]*\]/g, '')
              .trim()
              .match(/^\w+/)?.[0];
            if (!member) throw new Error(`Cannot parse ${name} member: ${declaration}`);
            return [member, explicitName ?? member[0]!.toLowerCase() + member.slice(1)];
          })
      );
      expect(Object.keys(expected).length).toBeGreaterThan(0);
      expect(values).toEqual(expected);
    });
  }
});
