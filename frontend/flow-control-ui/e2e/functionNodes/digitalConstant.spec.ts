import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'digitalConstant',
  configuration: { Value: true },
  vectors: [{ inputs: {}, expected: true }]
}));
