import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'toggle',
    pauseSimulation: true,
    vectors: [
      { inputs: { trigger: false }, expected: false },
      { inputs: { trigger: true }, expected: true },
      { inputs: { trigger: true }, expected: true },
      { inputs: { trigger: false }, expected: true },
      { inputs: { trigger: true }, expected: false },
      { inputs: { trigger: false }, expected: false },
      { inputs: { trigger: true }, expected: true }
    ]
  })
);
