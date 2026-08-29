import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    kind: 'add',
    vectors: [
      { inputs: { a: 2, b: 3 }, expected: 5, expectedError: false },
      { inputs: { a: 0, b: 0 }, expected: 0, expectedError: false },
      { inputs: { a: -4.5, b: 1.25 }, expected: -3.25, expectedError: false }
    ]
  })
);
