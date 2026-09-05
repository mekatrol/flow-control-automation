import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'not',
    vectors: [
      { inputs: { in: false }, expected: true },
      { inputs: { in: true }, expected: false }
    ]
  })
);
