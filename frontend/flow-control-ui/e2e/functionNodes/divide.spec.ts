import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'divide',
    vectors: [
      { inputs: { a: 9, b: 5 }, expected: 1.8, expectedError: false },
      { inputs: { a: -12, b: 3 }, expected: -4, expectedError: false }
    ]
  })
);

test(
  ...defineFunctionNodeTest({
    nodeType: 'divide',
    testLabel: 'divide by zero',
    vectors: [
      { inputs: { a: 9, b: 5 }, expected: 1.8, expectedError: false },
      { inputs: { a: 0, b: 0 }, expected: 1.8, expectedError: true },
      { inputs: { a: -12, b: 3 }, expected: -4, expectedError: false }
    ]
  })
);

test(
  ...defineFunctionNodeTest({
    nodeType: 'divide',
    testLabel: 'divide by zero from startup',
    vectors: [
      { inputs: { a: 0, b: 0 }, expected: 0.0, expectedError: true },
      { inputs: { a: -12, b: 3 }, expected: -4, expectedError: false }
    ]
  })
);
