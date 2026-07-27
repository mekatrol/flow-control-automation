// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AppSvg from '@/components/AppSvg.vue';

describe('AppSvg', () => {
  /**
   * Purpose: Protects the standard decorative, theme-aware SVG rendering contract.
   * Description: Renders a source with numeric sizing and observes its mask, inherited-color
   * presentation, accessibility state, and stable automation identifier.
   */
  it('renders a decorative SVG mask with configurable sizing', () => {
    const wrapper = mount(AppSvg, {
      props: {
        automation: 'save-icon',
        src: '/icons/save.svg',
        size: 24
      }
    });
    const icon = wrapper.get('.app-svg');

    // Expected outcome: The SVG source is rendered through a CSS mask.
    // Acceptance criteria: The mask references `/icons/save.svg` because external SVG
    // artwork must use the component's current-color theme treatment.
    expect(icon.attributes('style')).toContain('mask-image: url("/icons/save.svg")');

    // Expected outcome: Numeric size values use CSS pixel dimensions.
    // Acceptance criteria: Width and height are both `24px` because a single numeric size
    // must create a square icon with predictable layout dimensions.
    expect(icon.attributes('style')).toContain('width: 24px; height: 24px');

    // Expected outcome: An unlabeled icon is hidden from assistive technology.
    // Acceptance criteria: `aria-hidden` is `true` because decorative artwork must not add
    // redundant or meaningless content to the accessibility tree.
    expect(icon.attributes('aria-hidden')).toBe('true');

    // Expected outcome: The reusable icon exposes its required automation hook.
    // Acceptance criteria: The identifier is `save-icon` because callers need a stable
    // target that is independent of the selected SVG source.
    expect(icon.attributes('data-automation')).toBe('save-icon');
  });

  /**
   * Purpose: Ensures runtime icon and layout changes are reflected without remounting.
   * Description: Updates source, dimensions, fit, and color props after mount and observes
   * the resulting mask and presentation styles on the existing element.
   */
  it('reacts to runtime source and presentation changes', async () => {
    const wrapper = mount(AppSvg, {
      props: {
        automation: 'runtime-icon',
        src: '/icons/first.svg',
        size: 16
      }
    });

    await wrapper.setProps({
      src: '/icons/second.svg',
      width: '2rem',
      height: 30,
      color: 'var(--color-warning-text)',
      fit: 'cover'
    });
    const style = wrapper.get('.app-svg').attributes('style');

    // Expected outcome: Changing the source replaces the rendered SVG mask in place.
    // Acceptance criteria: The mask references `/icons/second.svg` because reactive callers
    // may change status icons without recreating the surrounding component.
    expect(style).toContain('mask-image: url("/icons/second.svg")');

    // Expected outcome: Width and height can be configured independently at runtime.
    // Acceptance criteria: Width is `2rem` and height is `30px` because string units must be
    // preserved while numeric dimensions are normalized to pixels.
    expect(style).toContain('width: 2rem; height: 30px');

    // Expected outcome: A runtime fit change affects the SVG mask presentation.
    // Acceptance criteria: Mask sizing is `cover` because callers must be able to fill
    // non-square dimensions without editing the source artwork.
    expect(style).toContain('mask-size: cover');

    // Expected outcome: A runtime color override uses the selected theme token.
    // Acceptance criteria: Color is `var(--color-warning-text)` because callers must be
    // able to select a semantic foreground that responds to the active theme.
    expect(style).toContain('color: var(--color-warning-text)');
  });

  /**
   * Purpose: Protects accessible use when an SVG conveys information rather than decoration.
   * Description: Supplies a label and observes image semantics and the absence of hiding.
   */
  it('exposes an informative SVG with an accessible label', () => {
    const wrapper = mount(AppSvg, {
      props: {
        automation: 'connection-status-icon',
        label: 'Connection healthy',
        src: '/icons/check.svg'
      }
    });
    const icon = wrapper.get('.app-svg');

    // Expected outcome: A labeled SVG is exposed as an accessible image.
    // Acceptance criteria: The role is `img` because the supplied label makes the icon
    // meaningful content rather than decorative artwork.
    expect(icon.attributes('role')).toBe('img');

    // Expected outcome: The accessible image uses the caller's description.
    // Acceptance criteria: The label is "Connection healthy" because assistive technology
    // must receive the same status meaning conveyed by the artwork.
    expect(icon.attributes('aria-label')).toBe('Connection healthy');

    // Expected outcome: Meaningful artwork remains in the accessibility tree.
    // Acceptance criteria: `aria-hidden` is absent because hiding a labeled status icon
    // would discard information explicitly supplied by the caller.
    expect(icon.attributes('aria-hidden')).toBeUndefined();
  });
});
