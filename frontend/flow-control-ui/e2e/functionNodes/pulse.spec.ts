import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'pulse',
    configuration: { 'Duration (ms)': 2000 },
    vectors: [
      { inputs: { input: false }, expected: false },
      { inputs: { input: true }, expectedBeforeAdvance: true, advanceMs: 1999, expected: true },
      { inputs: { input: true }, expectedBeforeAdvance: true, advanceMs: 1, expected: false },
      { inputs: { input: true }, expected: false },
      { inputs: { input: false }, expected: false },
      { inputs: { input: true }, expected: true }
    ]
  })
);
