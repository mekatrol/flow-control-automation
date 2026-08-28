import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'risingEdge',
  vectors: [
    { inputs: { in: false }, expected: false },
    { inputs: { in: true }, expected: true },
    { inputs: { in: true }, expected: false },
    { inputs: { in: false }, expected: false }
  ]
}));
