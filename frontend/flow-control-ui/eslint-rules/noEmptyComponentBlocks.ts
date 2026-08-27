import { ESLintUtils } from '@typescript-eslint/utils';

type Options = [];
type MessageIds = 'emptyBlock';
type ComponentBlockName = 'template' | 'script' | 'style';

type VueAttribute = {
  directive: boolean;
  key: {
    name: string;
  };
};

type VueElement = {
  type: 'VElement';
  name: string;
  loc: {
    start: { line: number; column: number };
    end: { line: number; column: number };
  };
  startTag: {
    attributes: VueAttribute[];
    range: [number, number];
  };
  endTag: {
    range: [number, number];
  } | null;
};

type VueDocumentFragment = {
  children: Array<VueElement | { type: string }>;
};

const COMPONENT_BLOCK_NAMES = new Set<ComponentBlockName>(['template', 'script', 'style']);
const createRule = ESLintUtils.RuleCreator((): string => '');

const noEmptyComponentBlocks = createRule<Options, MessageIds>({
  name: 'noEmptyComponentBlocks',

  meta: {
    type: 'problem',
    docs: {
      description: 'Disallow empty template, script, and style blocks in Vue components'
    },
    schema: [],
    messages: {
      emptyBlock: 'The <{{name}}> block is empty. Remove it or add content.'
    }
  },

  defaultOptions: [],

  create(context) {
    return {
      Program(): void {
        const sourceCode = context.sourceCode;
        const parserServices = sourceCode.parserServices as {
          getDocumentFragment?: () => VueDocumentFragment;
        };
        const documentFragment = parserServices.getDocumentFragment?.();

        if (!documentFragment) {
          return;
        }

        for (const child of documentFragment.children) {
          if (
            child.type !== 'VElement' ||
            !COMPONENT_BLOCK_NAMES.has(child.name as ComponentBlockName) ||
            !child.endTag
          ) {
            continue;
          }

          const hasExternalSource = child.startTag.attributes.some(
            (attribute) => !attribute.directive && attribute.key.name === 'src'
          );
          const contents = sourceCode.text.slice(child.startTag.range[1], child.endTag.range[0]);

          if (!hasExternalSource && contents.trim().length === 0) {
            context.report({
              loc: child.loc,
              messageId: 'emptyBlock',
              data: { name: child.name }
            });
          }
        }
      }
    };
  }
});

export default noEmptyComponentBlocks;
