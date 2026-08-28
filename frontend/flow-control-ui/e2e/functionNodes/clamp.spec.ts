import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'clamp',
  configuration: { Minimum: 0, Maximum: 100 },
  vectors: [
    { inputs: { input: -5 }, expected: 0 },
    { inputs: { input: 50 }, expected: 50 },
    { inputs: { input: 150 }, expected: 100 }
  ]
}));
