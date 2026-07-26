// @vitest-environment jsdom

import { nextTick, ref } from 'vue';
import { describe, expect, it } from 'vitest';

import { useModalFocus } from '@/features/flows/composables/useModalFocus';

describe('useModalFocus', () => {

  /**
   * Purpose: Protects the behavioral contract that moves focus into a modal, traps Tab, closes with Escape, and restores focus.
   * Description: Exercises moves focus into a modal, traps Tab, closes with Escape, and restores focus from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('moves focus into a modal, traps Tab, closes with Escape, and restores focus', async () => {
    const opener = document.createElement('button');
    const dialog = document.createElement('section');
    const first = document.createElement('button');
    const last = document.createElement('button');
    first.dataset.dialogInitialFocus = '';
    dialog.append(first, last);
    document.body.append(opener, dialog);
    opener.focus();

    const open = ref(false);
    const dialogRef = ref<HTMLElement>(dialog);
    const { handleKeydown } = useModalFocus(dialogRef, open, () => {
      open.value = false;
    });

    open.value = true;
    await nextTick();
    await nextTick();

    // Expected outcome: `document.activeElement` has the required value.
    // Acceptance criteria: `document.activeElement` must be `first`, because this condition proves that
    // moves focus into a modal, traps Tab, closes with Escape, and restores focus.
    expect(document.activeElement).toBe(first);

    last.focus();
    const tab = new KeyboardEvent('keydown', { key: 'Tab', cancelable: true });
    handleKeydown(tab);

    // Expected outcome: `tab.defaultPrevented` has the required value.
    // Acceptance criteria: `tab.defaultPrevented` must be `true`, because this condition proves that
    // moves focus into a modal, traps Tab, closes with Escape, and restores focus.
    expect(tab.defaultPrevented).toBe(true);

    // Expected outcome: `document.activeElement` has the required value.
    // Acceptance criteria: `document.activeElement` must be `first`, because this condition proves that
    // moves focus into a modal, traps Tab, closes with Escape, and restores focus.
    expect(document.activeElement).toBe(first);

    handleKeydown(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));
    await nextTick();
    await nextTick();

    // Expected outcome: `open.value` has the required value.
    // Acceptance criteria: `open.value` must be `false`, because this condition proves that
    // moves focus into a modal, traps Tab, closes with Escape, and restores focus.
    expect(open.value).toBe(false);

    // Expected outcome: `document.activeElement` has the required value.
    // Acceptance criteria: `document.activeElement` must be `opener`, because this condition proves that
    // moves focus into a modal, traps Tab, closes with Escape, and restores focus.
    expect(document.activeElement).toBe(opener);

    opener.remove();
    dialog.remove();
  });
});
