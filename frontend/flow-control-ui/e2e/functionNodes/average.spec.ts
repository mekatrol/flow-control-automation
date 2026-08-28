import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'average',
  vectors: [
    { inputs: { a: 2, b: 4 }, expected: 3 },
    { inputs: { a: -4.5, b: 1.5 }, expected: -1.5 },
    { inputs: { a: -4.5, b: -1.5 }, expected: -3.0 }
  ]
}));
