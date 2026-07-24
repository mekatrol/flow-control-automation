import type { Rule } from 'eslint';
import type { AST as VueAST } from 'vue-eslint-parser';

type VueParserServices = {
  defineTemplateBodyVisitor?: (
    templateVisitor: Rule.RuleListener,
    scriptVisitor?: Rule.RuleListener
  ) => Rule.RuleListener;
};

type RuleContext = Rule.RuleContext & {
  sourceCode: Rule.RuleContext['sourceCode'] & {
    parserServices: VueParserServices;
  };
};

// The core component types we do not want automation tags enforced when linting
const EXCLUDED_COMPONENTS = new Set([
  'CoreRequiredField',
  'CoreContentBlock',
  'CorePageLayout',
  'CoreFormPageLayout'
]);

// Must:
// - start with a lowercase letter
// - contain only lowercase letters, numbers and hyphens
// - be kebab-case (no leading/trailing/consecutive hyphens)
const AUTOMATION_PATTERN = /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/;

const requireAutomationProp: Rule.RuleModule = {
  meta: {
    type: 'problem',
    docs: {
      description: 'Require valid automation prop on UI core components'
    },
    schema: [],
    messages: {
      missingAutomation: '{{name}} requires an automation prop, e.g. automation="customer-name"',
      invalidAutomation:
        'automation value "{{value}}" must be lowercase kebab-case, start with a letter, and contain only letters, numbers, and hyphens'
    }
  },

  create(context): Rule.RuleListener {
    const typedContext = context as RuleContext;
    const parserServices = typedContext.sourceCode.parserServices;

    if (!parserServices?.defineTemplateBodyVisitor) {
      return {};
    }

    return parserServices.defineTemplateBodyVisitor({
      VElement(node: VueAST.VElement) {
        const name = node.rawName;

        if (EXCLUDED_COMPONENTS.has(name)) {
          return;
        }

        const isCoreComponent = name.startsWith('Core') || name.startsWith('Base');

        if (!isCoreComponent) {
          return;
        }

        const automationAttr = node.startTag.attributes.find(
          (attr): attr is VueAST.VAttribute =>
            attr.type === 'VAttribute' && attr.key.name === 'automation'
        );

        if (!automationAttr) {
          typedContext.report({
            node: node.startTag,
            messageId: 'missingAutomation',
            data: { name }
          });

          return;
        }

        // Only validate static string attributes.
        // Ignore bindings such as :automation="automationId".
        if (automationAttr.value && automationAttr.value.type === 'VLiteral') {
          const value = String(automationAttr.value.value);

          if (!AUTOMATION_PATTERN.test(value)) {
            typedContext.report({
              node: automationAttr,
              messageId: 'invalidAutomation',
              data: { value }
            });
          }
        }
      }
    });
  }
};

export default requireAutomationProp;
