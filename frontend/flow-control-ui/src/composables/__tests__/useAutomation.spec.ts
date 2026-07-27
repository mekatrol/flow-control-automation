import { describe, expect, it } from 'vitest';

import { useAutomation } from '@/composables/useAutomation';

describe('useAutomation', () => {
  /**
   * Purpose: Protects the behavioral contract that creates root and child automation attributes.
   * Description: Exercises creates root and child automation attributes from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('creates root and child automation attributes', () => {
    const automation = useAutomation('flow-table');

    // Expected outcome: `automation()` matches the required structure.
    // Acceptance criteria: `automation()` must supply both the required component prop
    // and `data-automation` as "flow-table", because one binding must support component
    // forwarding and native root metadata.
    // creates root and child automation attributes.
    expect(automation()).toEqual({
      automation: 'flow-table',
      'data-automation': 'flow-table'
    });

    // Expected outcome: `automation('next-page')` matches the required structure.
    // Acceptance criteria: `automation('next-page')` must provide the kebab-case
    // component prop "flow-table-next-page" and dotted DOM identifier
    // "flow-table.next-page", because both contracts describe the same child.
    expect(automation('next-page')).toEqual({
      automation: 'flow-table-next-page',
      'data-automation': 'flow-table.next-page'
    });
  });

  /**
   * Purpose: Protects the behavioral contract that rejects an invalid root name: %s.
   * Description: Exercises rejects an invalid root name: %s from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it.each(['FlowTable', 'flow.table', 'flow_table', '-flow-table'])(
    'rejects an invalid root name: %s',
    (name) => {
      // Expected outcome: The invalid operation is rejected.
      // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
      // creates root and child automation attributes.
      expect(() => useAutomation(name)()).toThrow(/lowercase kebab-case/);
    }
  );

  /**
   * Purpose: Protects the behavioral contract that rejects an invalid child name: %s.
   * Description: Exercises rejects an invalid child name: %s from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it.each(['NextPage', 'next.page', 'next_page', '-next-page'])(
    'rejects an invalid child name: %s',
    (name) => {
      const automation = useAutomation('flow-table');

      // Expected outcome: The invalid operation is rejected.
      // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
      // creates root and child automation attributes.
      expect(() => automation(name)).toThrow(/lowercase kebab-case/);
    }
  );
});
