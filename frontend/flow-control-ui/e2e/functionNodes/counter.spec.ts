import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'counter',
    testLabel: 'Counter with reset connected',
    vectors: [
      { inputs: { count: false, reset: false }, expected: 0 },
      { inputs: { count: true, reset: false }, expected: 1 },
      { inputs: { count: true, reset: false }, expected: 1 },
      { inputs: { count: false, reset: false }, expected: 1 },
      { inputs: { count: true, reset: false }, expected: 2 },
      { inputs: { count: false, reset: true }, expected: 0 },
      { inputs: { count: true, reset: true }, expected: 0 },
      { inputs: { count: false, reset: false }, expected: 0 },
      { inputs: { count: true, reset: false }, expected: 1 },
      { inputs: { count: false, reset: false }, expected: 1 },
      { inputs: { count: true, reset: true }, expected: 0 }
    ]
  })
);

test(
  ...defineFunctionNodeTest({
    nodeType: 'counter',
    testLabel: 'Counter with reset unconnected',
    unconnectedInputs: ['reset'],
    vectors: [
      { inputs: { count: false }, expected: 0 },
      { inputs: { count: true }, expected: 1 },
      { inputs: { count: true }, expected: 1 },
      { inputs: { count: false }, expected: 1 },
      { inputs: { count: true }, expected: 2 }
    ]
  })
);
