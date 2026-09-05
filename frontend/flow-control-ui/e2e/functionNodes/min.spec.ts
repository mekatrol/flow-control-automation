import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'min',
    vectors: [
      { inputs: { a: 2, b: 5 }, expected: 2 },
      { inputs: { a: -2, b: -5 }, expected: -5 }
    ]
  })
);
