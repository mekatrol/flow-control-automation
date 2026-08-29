import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';
test(...defineFunctionNodeTest({ kind: 'negate', vectors: [{ inputs: { in: 12.5 }, expected: -12.5 }, { inputs: { in: -3 }, expected: 3 }] }));
