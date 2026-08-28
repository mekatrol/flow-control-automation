import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'pulse',
  vectors: [
    { inputs: { input: false }, expected: false },
    { inputs: { input: true }, expected: true },
    { inputs: { input: true }, expected: false },
    { inputs: { input: false }, expected: false },
    { inputs: { input: true }, expected: true }
  ]
}));
