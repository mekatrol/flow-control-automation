// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it, vi } from 'vitest';
import { h } from 'vue';

import AppPromptDialog from '@/components/AppPromptDialog.vue';

describe('AppPromptDialog', () => {
  /**
   * Purpose: Protects the standard prompt content, semantic relationships, and automation contract.
   * Description: Exercises the fallback prompt and verifies its accessible information and generic actions.
   */
  it('renders the standard discard prompt', () => {
    const wrapper = mount(AppPromptDialog, {
      props: {
        automation: 'discard-prompt',
        contentLabel: 'Discard changes',
        id: 'discard-prompt'
      }
    });

    const section = wrapper.get('section');

    expect(section.attributes('aria-labelledby')).toBe('discard-prompt-title');
    expect(section.attributes('aria-describedby')).toBe('discard-prompt-description');
    expect(wrapper.get('h2').text()).toBe('Discard unsaved changes?');
    expect(wrapper.get('#discard-prompt-description').text()).toContain('will be lost');
    expect(wrapper.get('[data-automation="discard-prompt.cancel"]').text()).toContain(
      'Keep editing'
    );
    expect(wrapper.get('[data-automation="discard-prompt.confirm"]').text()).toContain(
      'Discard changes'
    );
  });

  /**
   * Purpose: Protects the generic cancellation and confirmation lifecycle.
   * Description: Exercises both standard actions and verifies they close the dialog and emit intent.
   */
  it('closes and emits generic prompt actions', async () => {
    const wrapper = mount(AppPromptDialog, {
      props: {
        contentLabel: 'Discard changes',
        id: 'discard-prompt'
      }
    });
    const close = vi.fn<() => void>();
    wrapper.get('dialog').element.close = close;

    await wrapper.get('[data-app-button]').trigger('click');

    expect(wrapper.emitted('cancel')).toHaveLength(1);
    expect(close).toHaveBeenCalledOnce();

    await wrapper.findAll('[data-app-button]')[1]!.trigger('click');

    expect(wrapper.emitted('confirm')).toHaveLength(1);
    expect(close).toHaveBeenCalledTimes(2);
  });

  /**
   * Purpose: Protects custom prompt rendering without losing access to the component lifecycle.
   * Description: Exercises the scoped prompt slot and invokes its generic cancel and confirm callbacks.
   */
  it('provides cancel and confirm callbacks to the prompt slot', async () => {
    const wrapper = mount(AppPromptDialog, {
      props: {
        contentLabel: 'Custom decision',
        id: 'custom-prompt'
      },
      slots: {
        prompt: ({ cancel, confirm }: { cancel: () => void; confirm: () => void }) =>
          h('div', [
            h('p', 'Custom prompt'),
            h('button', { id: 'custom-cancel', onClick: cancel }, 'No'),
            h('button', { id: 'custom-confirm', onClick: confirm }, 'Yes')
          ])
      }
    });
    wrapper.get('dialog').element.close = vi.fn<() => void>();

    await wrapper.get('#custom-cancel').trigger('click');
    await wrapper.get('#custom-confirm').trigger('click');

    expect(wrapper.text()).toContain('Custom prompt');
    expect(wrapper.emitted('cancel')).toHaveLength(1);
    expect(wrapper.emitted('confirm')).toHaveLength(1);
  });
});
