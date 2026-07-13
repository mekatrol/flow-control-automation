export type DesignerKeyboardCommand =
  | { type: 'move'; deltaX: number; deltaY: number }
  | { type: 'delete' };

interface KeyboardCommandEvent {
  key: string;
  target: EventTarget | null;
}

const isEditableTarget = (target: EventTarget | null): boolean => {
  if (!(target instanceof HTMLElement)) return false;
  const tagName = target.tagName.toLowerCase();
  return (
    target.isContentEditable ||
    target.contentEditable === 'true' ||
    tagName === 'input' ||
    tagName === 'textarea' ||
    tagName === 'select'
  );
};

export const interpretDesignerKey = (
  event: KeyboardCommandEvent,
  moveStep = 24
): DesignerKeyboardCommand | undefined => {
  // Designer shortcuts must not consume arrows, Backspace, or Delete while a
  // browser form control or editable region is using those keys for text entry.
  if (isEditableTarget(event.target)) return undefined;
  switch (event.key) {
    case 'ArrowLeft':
      return { type: 'move', deltaX: -moveStep, deltaY: 0 };
    case 'ArrowRight':
      return { type: 'move', deltaX: moveStep, deltaY: 0 };
    case 'ArrowUp':
      return { type: 'move', deltaX: 0, deltaY: -moveStep };
    case 'ArrowDown':
      return { type: 'move', deltaX: 0, deltaY: moveStep };
    case 'Backspace':
    case 'Delete':
      return { type: 'delete' };
    default:
      return undefined;
  }
};
