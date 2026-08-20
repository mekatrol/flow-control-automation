// @vitest-environment jsdom

import { mount, type VueWrapper } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { describe, expect, it, vi } from 'vitest';
import { useSaveShortcut } from '@/composables/useSaveShortcut';

const mountShortcut = (save: () => void, canSave: () => boolean = () => true): VueWrapper =>
  mount(
    defineComponent({
      setup() {
        useSaveShortcut(save, canSave);
        return () => null;
      }
    })
  );

describe('useSaveShortcut', () => {
  it.each([
    { ctrlKey: true, metaKey: false },
    { ctrlKey: false, metaKey: true }
  ])('prevents the browser shortcut and saves for $ctrlKey/$metaKey', (modifiers) => {
    const save = vi.fn<() => void>();
    const wrapper = mountShortcut(save);
    const event = new KeyboardEvent('keydown', {
      key: 's',
      cancelable: true,
      ...modifiers
    });

    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
    expect(save).toHaveBeenCalledOnce();
    wrapper.unmount();
  });

  it('prevents browser saving without invoking a disabled action', () => {
    const save = vi.fn<() => void>();
    const wrapper = mountShortcut(save, () => false);
    const event = new KeyboardEvent('keydown', { key: 's', ctrlKey: true, cancelable: true });

    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
    expect(save).not.toHaveBeenCalled();
    wrapper.unmount();
  });

  it('ignores modified alternatives and removes its listener on unmount', () => {
    const save = vi.fn<() => void>();
    const wrapper = mountShortcut(save);

    window.dispatchEvent(
      new KeyboardEvent('keydown', { key: 's', ctrlKey: true, shiftKey: true, cancelable: true })
    );
    wrapper.unmount();
    window.dispatchEvent(
      new KeyboardEvent('keydown', { key: 's', ctrlKey: true, cancelable: true })
    );

    expect(save).not.toHaveBeenCalled();
  });
});
