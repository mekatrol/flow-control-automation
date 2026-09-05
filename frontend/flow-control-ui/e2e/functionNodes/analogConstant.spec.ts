import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'analogConstant',
    configuration: { Value: 7.5 },
    vectors: [{ inputs: {}, expected: 7.5 }]
  })
);
