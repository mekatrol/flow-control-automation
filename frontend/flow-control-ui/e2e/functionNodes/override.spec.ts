import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'override',
  vectors: [
    { inputs: { input: false }, expected: false },
    { inputs: { input: true }, expected: true }
  ]
}));
