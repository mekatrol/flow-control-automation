import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'delay',
  configuration: { 'Duration (ms)': 2000 },
  vectors: [
    { inputs: { input: false }, expected: false },
    { inputs: { input: true }, expectedBeforeAdvance: false, advanceMs: 1999, expected: false },
    { inputs: { input: true }, expectedBeforeAdvance: false, advanceMs: 1, expected: true },
    { inputs: { input: false }, expectedBeforeAdvance: true, advanceMs: 1999, expected: true },
    { inputs: { input: false }, expectedBeforeAdvance: true, advanceMs: 1, expected: false }
  ]
}));
