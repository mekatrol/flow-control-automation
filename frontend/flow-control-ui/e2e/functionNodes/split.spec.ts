import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'split',
  vectors: [
    { inputs: { input: 0 }, expected: 0 },
    { inputs: { input: -4.25 }, expected: -4.25 }
  ]
}));
