import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'comparator',
    configuration: { Operator: 'gt' },
    vectors: [
      { inputs: { a: 5, b: 3 }, expected: true },
      { inputs: { a: 3, b: 3 }, expected: false },
      { inputs: { a: -2, b: 1 }, expected: false }
    ]
  })
);
