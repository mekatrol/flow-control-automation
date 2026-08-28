import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'calculator',
  vectors: [
    { inputs: { input: 12.5 }, expected: 12.5 },
    { inputs: { input: -3 }, expected: -3 }
  ]
}));
