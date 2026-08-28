import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'schedule',
  configuration: { Enabled: true },
  vectors: [{ inputs: {}, expected: true }]
}));
