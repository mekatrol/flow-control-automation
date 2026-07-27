// @vitest-environment jsdom

import { flushPromises, mount } from '@vue/test-utils';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import AppErrorNotice from '@/components/AppErrorNotice.vue';

describe('AppErrorNotice', () => {
  beforeEach(() => {
    HTMLDialogElement.prototype.showModal = vi.fn();
    HTMLDialogElement.prototype.close = vi.fn();
  });

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

    expect(HTMLDialogElement.prototype.showModal).toHaveBeenCalledOnce();
    expect(wrapper.get('[role="alert"]').text()).toContain('Request failed (502)');

    await wrapper.get('[data-automation="request-error-retry"]').trigger('click');

    expect(HTMLDialogElement.prototype.close).toHaveBeenCalledOnce();
    expect(wrapper.emitted('retry')).toHaveLength(1);
  });
});
