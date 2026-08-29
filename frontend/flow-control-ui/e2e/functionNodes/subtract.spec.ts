import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';
test(...defineFunctionNodeTest({ kind: 'subtract', vectors: [{ inputs: { a: 8, b: 3 }, expected: 5, expectedError: false }, { inputs: { a: -2, b: 4 }, expected: -6, expectedError: false }] }));
