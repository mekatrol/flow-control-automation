import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'schedule',
    configuration: { Enabled: true },
    vectors: [
      { inputs: { disable: false }, expected: true },
      { inputs: { disable: true }, expected: false },
      { inputs: { disable: false }, expected: true }
    ]
  })
);
