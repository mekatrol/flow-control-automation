import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'timer',
    configuration: { 'Duration (ms)': 0 },
    vectors: [
      { inputs: { input: false }, expected: false },
      { inputs: { input: true }, expected: true },
      { inputs: { input: false }, expected: false }
    ]
  })
);
