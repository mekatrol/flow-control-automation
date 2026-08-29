import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    kind: 'divide',
    vectors: [
      { inputs: { a: 9, b: 5 }, expected: 1.8 },
      { inputs: { a: -12, b: 3 }, expected: -4 }
    ]
  })
);

test(
  ...defineFunctionNodeTest({
    kind: 'divide',
    testLabel: 'divide by zero',
    vectors: [
      { inputs: { a: 0, b: 0 }, expected: 1.8 },
      { inputs: { a: -12, b: 3 }, expected: -4 }
    ]
  })
);
