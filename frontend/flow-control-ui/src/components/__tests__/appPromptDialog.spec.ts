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

    // Expected outcome: A confirmation prompt is exposed as an interruptive alert dialog.
    // Acceptance criteria: The native dialog role is `alertdialog` because the prompt
    // presents an important decision that must be resolved before work can continue.
    expect(wrapper.get('dialog').attributes('role')).toBe('alertdialog');

    // Expected outcome: The prompt title labels the standard prompt content.
    // Acceptance criteria: `aria-labelledby` references `discard-prompt-title` because
    // assistive technology must announce this prompt's visible heading as its title.
    expect(section.attributes('aria-labelledby')).toBe('discard-prompt-title');

    // Expected outcome: The prompt message describes the decision to the user.
    // Acceptance criteria: `aria-describedby` references `discard-prompt-description`
    // because the consequence text must be part of the prompt's accessible description.
    expect(section.attributes('aria-describedby')).toBe('discard-prompt-description');

    // Expected outcome: The fallback heading identifies the destructive decision.
    // Acceptance criteria: The heading is "Discard unsaved changes?" because callers
    // without a custom slot need an explicit warning before data is discarded.
    expect(wrapper.get('h2').text()).toBe('Discard unsaved changes?');

    // Expected outcome: The fallback description warns that changes will be lost.
    // Acceptance criteria: The description contains "will be lost" because a destructive
    // confirmation must explain the consequence before the user chooses an action.
    expect(wrapper.get('#discard-prompt-description').text()).toContain('will be lost');

    // Expected outcome: The safe fallback action lets the user continue editing.
    // Acceptance criteria: The cancel automation target contains "Keep editing" because
    // cancellation must preserve the unsaved work rather than imply destructive action.
    expect(wrapper.get('[data-automation="discard-prompt.cancel"]').text()).toContain(
      'Keep editing'
    );

    // Expected outcome: The destructive fallback action clearly confirms discarding.
    // Acceptance criteria: The confirm automation target contains "Discard changes"
    // because users must be able to distinguish the destructive choice from cancellation.
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
        automation: 'discard-prompt',
        contentLabel: 'Discard changes',
        id: 'discard-prompt'
      }
    });
    const close = vi.fn<() => void>();
    wrapper.get('dialog').element.close = close;

    await wrapper.get('[data-app-button]').trigger('click');

    // Expected outcome: Activating the safe action reports cancellation to the caller.
    // Acceptance criteria: One `cancel` event is emitted because one click must produce
    // exactly one cancellation decision without duplicate workflow handling.
    expect(wrapper.emitted('cancel')).toHaveLength(1);

    // Expected outcome: Cancelling dismisses the modal prompt.
    // Acceptance criteria: The native close method is called once because a completed
    // cancellation must return focus to the underlying workflow.
    expect(close).toHaveBeenCalledOnce();

    await wrapper.findAll('[data-app-button]')[1]!.trigger('click');

    // Expected outcome: Activating the destructive action reports confirmation.
    // Acceptance criteria: One `confirm` event is emitted because the caller needs one
    // unambiguous signal to perform the confirmed destructive operation.
    expect(wrapper.emitted('confirm')).toHaveLength(1);

    // Expected outcome: Both completed decisions dismiss the modal.
    // Acceptance criteria: The close method has two calls after one cancel and one confirm
    // because either terminal action must independently close the prompt.
    expect(close).toHaveBeenCalledTimes(2);
  });

  /**
   * Purpose: Protects custom prompt rendering without losing access to the component lifecycle.
   * Description: Exercises the scoped prompt slot and invokes its generic cancel and confirm callbacks.
   */
  it('provides cancel and confirm callbacks to the prompt slot', async () => {
    const wrapper = mount(AppPromptDialog, {
      props: {
        automation: 'custom-prompt',
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

    // Expected outcome: A supplied prompt slot replaces the fallback prompt content.
    // Acceptance criteria: The rendered text contains "Custom prompt" because callers
    // must be able to provide workflow-specific decision content.
    expect(wrapper.text()).toContain('Custom prompt');

    // Expected outcome: The custom slot can invoke the generic cancellation lifecycle.
    // Acceptance criteria: One `cancel` event is emitted because the slot's provided
    // callback must behave like the standard cancel action.
    expect(wrapper.emitted('cancel')).toHaveLength(1);

    // Expected outcome: The custom slot can invoke the generic confirmation lifecycle.
    // Acceptance criteria: One `confirm` event is emitted because the slot's provided
    // callback must behave like the standard confirm action.
    expect(wrapper.emitted('confirm')).toHaveLength(1);
  });

  /**
   * Purpose: Protects the imperative modal API and caller-defined wording used by
   * workflows that control AppPromptDialog through a component reference.
   * Description: Mounts a prompt with custom labels, invokes its exposed open and close
   * methods, and observes both the rendered wording and native dialog delegation.
   */
  it('supports custom wording and exposes the native modal controls', () => {
    const wrapper = mount(AppPromptDialog, {
      props: {
        automation: 'replace-prompt',
        cancelText: 'Retain value',
        confirmText: 'Replace value',
        contentLabel: 'Replace existing value',
        id: 'replace-prompt',
        message: 'The current value will be overwritten.',
        title: 'Replace existing value?'
      }
    });
    const dialog = wrapper.get('dialog').element as HTMLDialogElement;
    const showModal = vi.fn<() => void>();
    const close = vi.fn<() => void>();
    dialog.showModal = showModal;
    dialog.close = close;

    wrapper.vm.showModal();
    wrapper.vm.close();

    // Expected outcome: The custom prompt title replaces the destructive fallback title.
    // Acceptance criteria: The heading is "Replace existing value?" because callers must
    // be able to describe a workflow-specific decision without supplying a complete slot.
    expect(wrapper.get('h2').text()).toBe('Replace existing value?');

    // Expected outcome: The custom prompt message explains its workflow-specific consequence.
    // Acceptance criteria: The description is "The current value will be overwritten."
    // because the user must understand what confirmation changes in this arranged workflow.
    expect(wrapper.get('#replace-prompt-description').text()).toBe(
      'The current value will be overwritten.'
    );

    // Expected outcome: The custom safe action uses the caller's terminology.
    // Acceptance criteria: The first action contains "Retain value" because cancellation
    // preserves the existing value in this replacement workflow.
    expect(wrapper.findAll('[data-app-button]')[0]!.text()).toContain('Retain value');

    // Expected outcome: The custom confirmation action uses the caller's terminology.
    // Acceptance criteria: The second action contains "Replace value" because confirmation
    // authorizes replacement in this workflow rather than generic discarding.
    expect(wrapper.findAll('[data-app-button]')[1]!.text()).toContain('Replace value');

    // Expected outcome: The exposed open control delegates to the native modal.
    // Acceptance criteria: `showModal` is called once because one imperative open request
    // must create one native modal presentation.
    expect(showModal).toHaveBeenCalledOnce();

    // Expected outcome: The exposed close control delegates to the native modal.
    // Acceptance criteria: `close` is called once because one imperative close request
    // must end the modal without emitting a user decision.
    expect(close).toHaveBeenCalledOnce();
  });
});
