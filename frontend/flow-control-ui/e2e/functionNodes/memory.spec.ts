import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'memory',
  configuration: { 'Initial value': 2 },
  vectors: [
    { inputs: { in: 7 }, expected: 7 },
    { inputs: { in: -3 }, expected: -3 },
    { inputs: { in: 0.5 }, expected: 0.5 }
  ]
}));
