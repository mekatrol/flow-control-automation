import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'levelShifter',
  configuration: { Gain: 2, Offset: 1 },
  vectors: [
    { inputs: { in: 3 }, expected: 7 },
    { inputs: { in: -2 }, expected: -3 }
  ]
}));
