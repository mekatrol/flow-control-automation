import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';
test(...defineFunctionNodeTest({ kind: 'power', vectors: [{ inputs: { a: 2, b: 8 }, expected: 256 }, { inputs: { a: 9, b: 0.5 }, expected: 3 }] }));
