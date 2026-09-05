import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'average',
    vectors: [
      { inputs: { a: 2, b: 4 }, expected: 3, expectedError: false },
      { inputs: { a: -4.5, b: 1.5 }, expected: -1.5, expectedError: false },
      { inputs: { a: -4.5, b: -1.5 }, expected: -3.0, expectedError: false }
    ]
  })
);
