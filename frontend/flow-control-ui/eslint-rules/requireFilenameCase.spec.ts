import type { ESLint } from 'eslint';
import { Linter } from 'eslint';
import { describe, expect, it } from 'vitest';

import requireFilenameCase from './requireFilenameCase.js';

const lintFilename = (filename: string): Linter.LintMessage[] => {
  const linter = new Linter();
  const localPlugin = {
    rules: {
      requireFilenameCase
    }
  } as unknown as ESLint.Plugin;

  return linter.verify(
    'const example = true;',
    {
      files: ['**/*.ts'],
      plugins: {
        local: localPlugin
      },
      rules: {
        'local/requireFilenameCase': 'error'
      }
    },
    { filename }
  );
};

describe('requireFilenameCase', () => {
  it('requires component implementation files to use PascalCase', () => {
    expect(lintFilename('src/components/themeSelector.ts')).toEqual([
      expect.objectContaining({
        messageId: 'expectedPascalCase'
      })
    ]);
    expect(lintFilename('src/components/ThemeSelector.ts')).toEqual([]);
  });

  it('requires component test files to use camelCase', () => {
    expect(lintFilename('src/components/__tests__/ThemeSelector.spec.ts')).toEqual([
      expect.objectContaining({
        messageId: 'expectedCamelCase'
      })
    ]);
    expect(lintFilename('src/components/__tests__/themeSelector.spec.ts')).toEqual([]);
  });
});
