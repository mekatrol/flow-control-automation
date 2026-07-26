import { describe, expect, it } from 'vitest';

import { useAutomation } from '@/composables/useAutomation';

describe('useAutomation', () => {
  it('creates root and child automation attributes', () => {
    const automation = useAutomation('flow-table');

    expect(automation()).toEqual({ 'data-automation': 'flow-table' });
    expect(automation('next-page')).toEqual({
      'data-automation': 'flow-table.next-page'
    });
  });

  it.each(['FlowTable', 'flow.table', 'flow_table', '-flow-table'])(
    'rejects an invalid root name: %s',
    (name) => {
      expect(() => useAutomation(name)()).toThrow(/lowercase kebab-case/);
    }
  );

  it.each(['NextPage', 'next.page', 'next_page', '-next-page'])(
    'rejects an invalid child name: %s',
    (name) => {
      const automation = useAutomation('flow-table');

      expect(() => automation(name)).toThrow(/lowercase kebab-case/);
    }
  );
});
