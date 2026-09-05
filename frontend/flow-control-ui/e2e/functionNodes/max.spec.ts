import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'max',
    vectors: [
      { inputs: { a: 2, b: 5 }, expected: 5 },
      { inputs: { a: -2, b: -5 }, expected: -2 }
    ]
  })
);
