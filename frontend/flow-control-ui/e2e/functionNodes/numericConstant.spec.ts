import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'numericConstant',
  configuration: { Value: 7.5 },
  vectors: [{ inputs: {}, expected: 7.5 }]
}));
