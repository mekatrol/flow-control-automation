import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'counter',
  vectors: [
    { inputs: { count: false, reset: false }, expected: 0 },
    { inputs: { count: true, reset: false }, expected: 1 },
    { inputs: { count: true, reset: false }, expected: 1 },
    { inputs: { count: false, reset: false }, expected: 1 },
    { inputs: { count: true, reset: false }, expected: 2 },
    { inputs: { count: false, reset: true }, expected: 0 },
    { inputs: { count: true, reset: true }, expected: 1 },
    { inputs: { count: false, reset: false }, expected: 1 },
    { inputs: { count: true, reset: false }, expected: 2 },
    { inputs: { count: false, reset: false }, expected: 2 },
    { inputs: { count: true, reset: true }, expected: 0 }
  ]
}));
