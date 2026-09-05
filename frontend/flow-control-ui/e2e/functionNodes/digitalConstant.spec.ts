import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'digitalConstant',
    configuration: { Value: true },
    vectors: [{ inputs: {}, expected: true }]
  })
);
