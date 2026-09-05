import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    nodeType: 'clock',
    configuration: { 'Frequency (Hz)': 2, 'Duty cycle (%)': 25 },
    vectors: [
      { inputs: { enable: false }, expected: false },
      { inputs: { enable: true }, expectedBeforeAdvance: true, advanceMs: 124, expected: true },
      { inputs: { enable: true }, expectedBeforeAdvance: true, advanceMs: 1, expected: false },
      { inputs: { enable: true }, expectedBeforeAdvance: false, advanceMs: 375, expected: true },
      { inputs: { enable: false }, expected: false },
      { inputs: { enable: true }, expected: true }
    ]
  })
);
