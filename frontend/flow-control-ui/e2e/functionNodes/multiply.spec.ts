import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';
test(...defineFunctionNodeTest({ kind: 'multiply', vectors: [{ inputs: { a: 6, b: 7 }, expected: 42, expectedError: false }, { inputs: { a: -2.5, b: 4 }, expected: -10, expectedError: false }] }));
