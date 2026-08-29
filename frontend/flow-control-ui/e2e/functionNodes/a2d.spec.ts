import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'a2d',
  configuration: { 'Active low threshold': 25, 'Active high threshold': 75 },
  vectors: [
    { inputs: { in: 50 }, expected: false },
    { inputs: { in: 75 }, expected: true },
    { inputs: { in: 50 }, expected: true },
    { inputs: { in: 25 }, expected: false },
    { inputs: { in: 50 }, expected: false }
  ]
}));
