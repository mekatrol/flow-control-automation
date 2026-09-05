import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'd2a',
    configuration: { 'Low analog value': -10, 'High analog value': 20 },
    vectors: [
      { inputs: { in: false }, expected: -10 },
      { inputs: { in: true }, expected: 20 },
      { inputs: { in: false }, expected: -10 }
    ]
  })
);
