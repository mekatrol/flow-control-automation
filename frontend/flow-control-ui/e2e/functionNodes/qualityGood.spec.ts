import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'qualityGood',
    vectors: [
      { inputs: { in: 0 }, expected: true },
      { inputs: { in: -12.5 }, expected: true }
    ]
  })
);
