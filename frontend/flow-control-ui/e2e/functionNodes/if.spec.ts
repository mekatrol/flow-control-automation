import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest({
  kind: 'if',
  vectors: [
    { inputs: { condition: true, whenTrue: true, whenFalse: false }, expected: true },
    { inputs: { condition: false, whenTrue: true, whenFalse: false }, expected: false },
    { inputs: { condition: true, whenTrue: false, whenFalse: true }, expected: false }
  ]
}));
