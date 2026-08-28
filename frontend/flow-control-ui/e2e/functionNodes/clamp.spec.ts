import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(
  ...defineFunctionNodeTest({
    kind: 'clamp',
    testLabel: 'clamp [positive min, positive max]',
    configuration: { Minimum: 0, Maximum: 100 },
    vectors: [
      { inputs: { input: -5 }, expected: 0 },
      { inputs: { input: 50 }, expected: 50 },
      { inputs: { input: 150 }, expected: 100 }
    ]
  })
);

test(
  ...defineFunctionNodeTest({
    kind: 'clamp',
    testLabel: 'clamp [negative min, positive max]',
    configuration: { Minimum: -100, Maximum: 100 },
    vectors: [
      { inputs: { input: -101 }, expected: -100 },
      { inputs: { input: -5 }, expected: -5 },
      { inputs: { input: -150 }, expected: -100 },
      { inputs: { input: 150 }, expected: 100 }
    ]
  })
);

test(
  ...defineFunctionNodeTest({
    kind: 'clamp',
    testLabel: 'clamp [negative min, negative max]',
    configuration: { Minimum: -100, Maximum: -1 },
    vectors: [
      { inputs: { input: -101 }, expected: -100 },
      { inputs: { input: -5 }, expected: -5 },
      { inputs: { input: 150 }, expected: -1 },
      { inputs: { input: 0 }, expected: -1 }
    ]
  })
);
