// @vitest-environment jsdom

import { flushPromises, mount } from '@vue/test-utils';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import AppErrorNotice from '@/components/AppErrorNotice.vue';

describe('AppErrorNotice', () => {
  let showModal: ReturnType<typeof vi.fn<() => void>>;
  let close: ReturnType<typeof vi.fn<() => void>>;

  beforeEach(() => {
    showModal = vi.fn<() => void>();
    close = vi.fn<() => void>();
    HTMLDialogElement.prototype.showModal = showModal;
    HTMLDialogElement.prototype.close = close;
  });

  /**
   * Purpose: Protects the error-notice lifecycle that announces a new failure and lets
   * the user request recovery.
   * Description: Supplies an error after mounting, activates Retry, and verifies that
   * the modal opens, displays the failure, closes, and emits one retry request.
   */
  it('opens AppNotice when an error is received and emits retry', async () => {
    const wrapper = mount(AppErrorNotice, {
      props: {
        id: 'request-error',
        automation: 'request-error',
        message: '',
        retryable: true
      }
    });

    await wrapper.setProps({ message: 'Request failed (502)' });
    await flushPromises();

    // Expected outcome: Receiving an error opens the notice as a modal.
    // Acceptance criteria: showModal is called once because one newly supplied failure
    // must result in one visible, announced error presentation.
    expect(showModal).toHaveBeenCalledOnce();

    // Expected outcome: The opened notice presents the server failure.
    // Acceptance criteria: The alert contains "Request failed (502)" because the user
    // needs the supplied error detail to understand what failed.
    expect(wrapper.get('[role="alert"]').text()).toContain('Request failed (502)');

    await wrapper.get('[data-automation="request-error.retry"]').trigger('click');

    // Expected outcome: Activating Retry dismisses the current error notice.
    // Acceptance criteria: close is called once because the recovery action must remove
    // the modal before the parent begins another request.
    expect(close).toHaveBeenCalledOnce();

    // Expected outcome: Activating Retry reports one recovery request to the parent.
    // Acceptance criteria: Exactly one retry event is emitted because one user action
    // must trigger one replacement request without duplication.
    expect(wrapper.emitted('retry')).toHaveLength(1);
  });
});
