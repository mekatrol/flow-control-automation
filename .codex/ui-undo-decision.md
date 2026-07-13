# Undo and redo decision

Undo/redo is not required for the first complete UI migration.

The current authoring safety model provides explicit save, graph-level dirty
state, a warning before route or browser navigation, and an explicit discard
path that restores the last server-confirmed graph. That covers accidental
navigations and abandoned editing sessions without introducing a command
history across pointer drags, configuration inputs, node creation, connections,
and asynchronous API replacement.

This decision should be revisited after real authoring feedback. If undo/redo is
added later, it must use bounded serialisable graph snapshots or typed commands,
coalesce pointer-move updates into one history entry, exclude runtime status,
and include the unit and browser coverage described in the migration plan.
