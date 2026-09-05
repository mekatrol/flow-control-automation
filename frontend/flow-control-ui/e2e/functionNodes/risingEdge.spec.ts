import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'risingEdge',
    // The output is a one-scan pulse. Keep the simulator paused between
    // apply-and-step calls so the UI poll cannot miss it.
    pauseSimulation: true,
    vectors: [
      { inputs: { in: false }, expected: false },
      { inputs: { in: true }, expected: true },
      { inputs: { in: true }, expected: false },
      { inputs: { in: false }, expected: false }
    ]
  })
);
