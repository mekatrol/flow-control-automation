import { nextTick, type Ref, watch } from 'vue';

const focusableSelector = [
  'button:not([disabled])',
  '[href]',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])'
].join(',');

export const useModalFocus = (
  dialog: Ref<HTMLElement | undefined>,
  open: Ref<boolean>,
  close: () => void
): { handleKeydown: (event: KeyboardEvent) => void } => {
  let returnFocusTo: HTMLElement | undefined;

  watch(open, (isOpen) => {
    if (isOpen) {
      // Remember the control that opened the modal so closing it returns the
      // keyboard user to a predictable point in the page rather than the body.
      returnFocusTo =
        document.activeElement instanceof HTMLElement ? document.activeElement : undefined;
      void nextTick(() => {
        const initialFocus = dialog.value?.querySelector<HTMLElement>(
          '[data-dialog-initial-focus]'
        );
        (initialFocus ?? dialog.value)?.focus();
      });
      return;
    }

    void nextTick(() => returnFocusTo?.focus());
  });

  const handleKeydown = (event: KeyboardEvent): void => {
    if (event.key === 'Escape') {
      event.preventDefault();
      close();
      return;
    }
    if (event.key !== 'Tab' || !dialog.value) return;

    // aria-modal tells assistive technology that background content is inactive;
    // cycling Tab inside the same surface makes that promise true for keyboards.
    const controls = [...dialog.value.querySelectorAll<HTMLElement>(focusableSelector)].filter(
      (element) => !element.hidden
    );
    if (controls.length === 0) {
      event.preventDefault();
      dialog.value.focus();
      return;
    }
    const first = controls[0]!;
    const last = controls.at(-1)!;
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return { handleKeydown };
};
