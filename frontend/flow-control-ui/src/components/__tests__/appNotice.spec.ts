// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { h } from 'vue';
import { describe, expect, it, vi } from 'vitest';

import AppNotice, { type AppNoticeVariant } from '@/components/AppNotice.vue';

describe('AppNotice', () => {
  /**
   * Purpose: Protects the accessible fallback presentation for every supported notice severity.
   * Description: Renders each variant with standard content and observes its semantics, icon,
   * title, message, and stable automation targets.
   */
  it.each([
    ['info', 'status'],
    ['debug', 'status'],
    ['warning', 'status'],
    ['error', 'alert']
  ] satisfies [AppNoticeVariant, string][])('renders the %s variant', (variant, expectedRole) => {
    const wrapper = mount(AppNotice, {
      props: {
        automation: 'request-error',
        id: 'request-error',
        message: 'The request could not be completed.',
        title: 'Request failed',
        variant
      }
    });

    // Expected outcome: The notice uses urgency semantics appropriate to its severity.
    // Acceptance criteria: The role is `alert` only for errors and `status` otherwise
    // because urgent failures require interruption while lower severities do not.
    expect(wrapper.get('article').attributes('role')).toBe(expectedRole);

    // Expected outcome: The visible heading identifies the notice.
    // Acceptance criteria: The heading is "Request failed" because the caller-provided
    // title must label the dialog and its notice content.
    expect(wrapper.get('h2').text()).toBe('Request failed');

    // Expected outcome: The fallback body shows the caller's error detail.
    // Acceptance criteria: The content target contains the arranged request failure because
    // callers need a useful message without providing a custom content slot.
    expect(wrapper.get('[data-automation="request-error.content"]').text()).toBe(
      'The request could not be completed.'
    );

    // Expected outcome: Each severity supplies a decorative SVG visual indicator.
    // Acceptance criteria: The icon source is an encoded SVG because each selected variant
    // must have a scalable visual cue while its meaning remains available in visible text.
    expect(wrapper.get('img').attributes('src')).toContain('image/svg+xml');
  });

  /**
   * Purpose: Ensures rich error details can contain links without leaking markup to the clipboard.
   * Description: Supplies linked content, activates the standard copy action, and observes the
   * plain-text clipboard payload and success announcement.
   */
  it('copies rendered rich content as plain text', async () => {
    const writeText = vi.fn<(text: string) => Promise<void>>().mockResolvedValue();
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText }
    });
    const wrapper = mount(AppNotice, {
      props: {
        automation: 'api-error',
        id: 'api-error',
        message: 'Fallback detail',
        title: 'API error'
      },
      slots: {
        content: () =>
          h('p', ['See ', h('a', { href: '/logs' }, 'request logs'), ' for more information.'])
      }
    });

    await wrapper.get('[data-automation="api-error.copy"]').trigger('click');

    // Expected outcome: Copying rich content writes only its human-readable text.
    // Acceptance criteria: The clipboard receives the exact rendered sentence with no anchor
    // markup because copied diagnostics must be safe for plain-text destinations.
    expect(writeText).toHaveBeenCalledWith('See request logs for more information.');

    // Expected outcome: Successful copying is announced without moving focus.
    // Acceptance criteria: The live status says details were copied because keyboard and
    // assistive-technology users need confirmation that the asynchronous action completed.
    expect(wrapper.get('[aria-live="polite"]').text()).toBe('Details copied to clipboard.');
  });

  /**
   * Purpose: Protects caller control over all three content regions and their action callbacks.
   * Description: Replaces the header, content, and footer slots, invokes the supplied close
   * callback, and observes custom rendering plus native dialog delegation.
   */
  it('supports custom header, content, and footer slots', async () => {
    const wrapper = mount(AppNotice, {
      props: {
        automation: 'custom-error',
        id: 'custom-error',
        message: 'Fallback message',
        title: 'Fallback title'
      },
      slots: {
        header: ({ title }: { title: string }) => h('h2', `Custom ${title}`),
        content: () => h('a', { href: '/support' }, 'Contact support'),
        footer: ({ close }: { close: () => void }) =>
          h('button', { id: 'custom-close', onClick: close }, 'Dismiss custom notice')
      }
    });
    const close = vi.fn<() => void>();
    wrapper.get('dialog').element.close = close;

    await wrapper.get('#custom-close').trigger('click');

    // Expected outcome: The caller's three regions replace all fallback content.
    // Acceptance criteria: The rendered text contains the custom title, link, and dismissal
    // label because each named slot must independently support workflow-specific presentation.
    expect(wrapper.text()).toContain('Custom Fallback titleContact supportDismiss custom notice');

    // Expected outcome: A custom footer can dismiss the underlying modal.
    // Acceptance criteria: Native `close` is called once because the footer's supplied close
    // callback must preserve AppNotice's standard dialog lifecycle.
    expect(close).toHaveBeenCalledOnce();
  });

  /**
   * Purpose: Protects imperative presentation for callers that own when a notice becomes visible.
   * Description: Invokes the exposed show and close methods and observes delegation to AppDialog.
   */
  it('exposes modal lifecycle methods', () => {
    const wrapper = mount(AppNotice, {
      props: {
        automation: 'save-error',
        id: 'save-error',
        message: 'Try again.',
        title: 'Save failed'
      }
    });
    const dialog = wrapper.get('dialog').element as HTMLDialogElement;
    const showModal = vi.fn<() => void>();
    const close = vi.fn<() => void>();
    dialog.showModal = showModal;
    dialog.close = close;

    wrapper.vm.showModal();
    wrapper.vm.close();

    // Expected outcome: Opening AppNotice presents the native modal overlay.
    // Acceptance criteria: `showModal` is called once because one caller request must create
    // one modal presentation with browser-managed focus and backdrop behavior.
    expect(showModal).toHaveBeenCalledOnce();

    // Expected outcome: Closing AppNotice dismisses the native modal overlay.
    // Acceptance criteria: `close` is called once because one caller request must return
    // control and focus to the underlying view.
    expect(close).toHaveBeenCalledOnce();
  });
});
