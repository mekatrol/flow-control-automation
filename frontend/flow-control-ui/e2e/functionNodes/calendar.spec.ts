import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'calendar',
    configuration: { Enabled: true },
    vectors: [{ inputs: {}, expected: true }]
  })
);
