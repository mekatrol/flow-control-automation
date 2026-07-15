// @vitest-environment jsdom

import { nextTick, ref } from 'vue';
import { describe, expect, it } from 'vitest';

import { useModalFocus } from '@/features/flows/composables/useModalFocus';

describe('useModalFocus', () => {
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
    expect(document.activeElement).toBe(first);

    last.focus();
    const tab = new KeyboardEvent('keydown', { key: 'Tab', cancelable: true });
    handleKeydown(tab);
    expect(tab.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(first);

    handleKeydown(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));
    await nextTick();
    await nextTick();
    expect(open.value).toBe(false);
    expect(document.activeElement).toBe(opener);

    opener.remove();
    dialog.remove();
  });
});
