import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';
test(
  ...defineFunctionNodeTest({
    nodeType: 'power',
    vectors: [
      { inputs: { a: 2, b: 8 }, expected: 256, expectedError: false },
      { inputs: { a: 9, b: 0.5 }, expected: 3, expectedError: false }
    ]
  })
);
