import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'calendar',
  configuration: { Enabled: true },
  vectors: [{ inputs: {}, expected: true }]
}));
