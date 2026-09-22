import { defineFunctionNodeTest, test } from './helpers/functionNodeCase';

const activePeriod = [{ on: '00:00', off: '23:59' }];
const alwaysActiveSchedule = JSON.stringify({
  monday: activePeriod,
  tuesday: activePeriod,
  wednesday: activePeriod,
  thursday: activePeriod,
  friday: activePeriod,
  saturday: activePeriod,
  sunday: activePeriod
});

test(
  ...defineFunctionNodeTest({
    nodeType: 'schedule',
    configuration: {
      Enabled: true,
      'Weekly schedule': alwaysActiveSchedule
    },
    vectors: [
      { inputs: { disable: false }, expected: true },
      { inputs: { disable: true }, expected: false },
      { inputs: { disable: false }, expected: true }
    ]
  })
);
