import type { ESLint } from 'eslint';
import { Linter } from 'eslint';
import { describe, expect, it } from 'vitest';
import vueParser from 'vue-eslint-parser';

import noEmptyComponentBlocks from './noEmptyComponentBlocks.js';

const lintComponent = (source: string): Linter.LintMessage[] => {
  const linter = new Linter();
  const localPlugin = {
    rules: {
      noEmptyComponentBlocks
    }
  } as unknown as ESLint.Plugin;

  return linter.verify(
    source,
    {
      files: ['**/*.vue'],
      languageOptions: {
        parser: vueParser
      },
      plugins: {
        local: localPlugin
      },
      rules: {
        'local/noEmptyComponentBlocks': 'error'
      }
    },
    { filename: 'AppExample.vue' }
  );
};

describe('noEmptyComponentBlocks', () => {
  it.each(['template', 'script', 'style'])('reports an empty <%s> block', (name) => {
    const messages = lintComponent(`<${name}>\n  \n</${name}>`);

    expect(messages).toEqual([
      expect.objectContaining({
        messageId: 'emptyBlock',
        severity: 2
      })
    ]);
  });

  it('reports an empty script setup block with attributes', () => {
    const messages = lintComponent('<script setup lang="ts"></script>');

    expect(messages).toEqual([
      expect.objectContaining({
        message: 'The <script> block is empty. Remove it or add content.'
      })
    ]);
  });

  it.each([
    '<template><main /></template>',
    '<script setup lang="ts">// Component setup</script>',
    '<style>/* Component styles */</style>',
    '<template src="./component.html"></template>',
    '<script src="./component.ts"></script>',
    '<style src="./component.css"></style>'
  ])('allows a meaningful component block: %s', (source) => {
    expect(lintComponent(source)).toEqual([]);
  });
});
