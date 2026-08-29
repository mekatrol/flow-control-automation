import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';
test(...defineFunctionNodeTest({ kind: 'negate', vectors: [{ inputs: { in: 12.5 }, expected: -12.5, expectedError: false }, { inputs: { in: -3 }, expected: 3, expectedError: false }] }));
