import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'schedule',
    configuration: { Enabled: true },
    vectors: [{ inputs: {}, expected: true }]
  })
);
