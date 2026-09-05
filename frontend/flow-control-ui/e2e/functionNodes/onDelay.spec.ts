import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'onDelay',
    configuration: { 'Duration (ms)': 0 },
    vectors: [
      { inputs: { in: false }, expected: false },
      { inputs: { in: true }, expected: true },
      { inputs: { in: false }, expected: false }
    ]
  })
);
