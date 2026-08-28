import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'selector',
  vectors: [
    { inputs: { condition: true, a: 10, b: 20 }, expected: 10 },
    { inputs: { condition: false, a: 10, b: 20 }, expected: 20 }
  ]
}));
