import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'line',
  configuration: { Gain: 0.5, Offset: 2 },
  vectors: [
    { inputs: { input: 8 }, expected: 6 },
    { inputs: { input: -4 }, expected: 0 }
  ]
}));
