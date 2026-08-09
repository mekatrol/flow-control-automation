#!/usr/bin/env python3
"""Apply repository-specific C formatting rules after ``clang-format``.

Purpose
=======

``clang-format`` remains the primary C formatter for this repository.  This
module is a deliberately small post-formatter for preferences that
``clang-format`` cannot express.  The ``format`` task runs ``clang-format``
first and then invokes this script once for every tracked or untracked,
non-ignored ``.c`` and ``.h`` file.

The current bespoke rules are:

1. Put a blank line before a control-flow statement that opens an ``if``,
   ``for``, ``while``, or ``switch`` block, except when the statement is the
   first item after an opening brace.  If a contiguous comment immediately
   precedes the statement, put the blank line before that comment so the
   comment remains visually attached to the statement it documents.
2. Change a discarded function call such as ``(void)read_value();`` to the
   ordinary expression statement ``read_value();``.  Other uses of ``void``,
   including pointer casts and unused-variable suppressions, are untouched.

Design boundary
===============

This module is line-oriented and is not a C parser.  That is intentional: it
runs after ``clang-format``, so the input has a predictable layout, and each
implemented rule can be recognized safely from the beginning of one line.
Regular expressions in this file must remain conservative.  A transformation
should prefer missing an unusual spelling over changing code whose meaning is
uncertain.

In particular, the formatter currently assumes:

* control keywords begin their formatted source line;
* discarded calls begin with ``(void)`` followed by a simple C identifier;
* comments attached to control statements occupy contiguous, comment-leading
  lines; and
* an opening brace at the end of the previous line identifies the first item
  in a block.

These assumptions match the repository's ``.clang-format`` output.  They also
keep strings, macros, nested casts, function-pointer calls, and general C
expressions outside the rewrite surface.  If a future preference needs token
awareness—for example, distinguishing code from text inside a multi-line
macro—add a lexer/parser layer and corresponding regression tests instead of
broadening a regular expression until it accepts ambiguous syntax.

Maintenance workflow
====================

For every new bespoke rule:

* document the user-visible contract in ``AGENTS.md``;
* add a narrowly named pattern and transformation here;
* explain why the transformation is safe after ``clang-format``;
* add positive, negative, boundary, and idempotence cases to
  ``tests/test_format_source.py``;
* run the repository format task twice and confirm the second pass produces no
  diff; and
* run the host build and tests because formatting changes every source file in
  scope.

The public program interface is intentionally minimal: each command-line
argument is a UTF-8 source path to rewrite in place, and success returns zero.
File-system and decoding failures are allowed to propagate so the surrounding
format task fails loudly instead of leaving a partially formatted tree without
notice.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path


# A block-opening control statement is eligible for vertical separation.  The
# pattern is anchored to the start of a clang-formatted line so constructs such
# as ``else if``, ``} while (...)``, macro bodies, and text later on a line do
# not accidentally match.  The indentation capture is descriptive and leaves
# room for future transformations even though the spacing rule only tests the
# match today.
CONTROL_BLOCK = re.compile(r"^(?P<indent>\s*)(?:if|for|while|switch)\s*\(")

# A discarded-result cast is removable only when it starts a statement and is
# followed by a direct call through a simple identifier.  The lookahead checks
# the call without consuming it; replacement can therefore remove exactly the
# cast while retaining indentation and all original call text.  This excludes
# ``(void)value``, ``(void *)pointer``, nested casts, member syntax, and
# function-pointer expressions because those forms require semantic judgment.
DISCARDED_CALL_CAST = re.compile(r"^(?P<indent>\s*)\(void\)(?=\s*[A-Za-z_]\w*\s*\()")

# Comment-leading lines form the only comment blocks moved by the spacing rule.
# This recognizes C++-style comments, the first and interior lines of common C
# block-comment layouts, and a standalone terminator.  Trailing comments are
# deliberately excluded because they document the preceding code rather than
# the following control statement.
COMMENT_LINE = re.compile(r"^\s*(?://|/\*|\*|\*/)")


# Tests whether a source line belongs to a comment block that may document the
# following control statement.  The caller must supply one line without its
# newline terminator; the result is true only when the first non-whitespace
# token is recognized as comment syntax.
def is_comment_line(line: str) -> bool:
    return COMMENT_LINE.match(line) is not None


# Gets the insertion index for whitespace associated with a control statement.
# ``statement_index`` must identify the next position after the already
# formatted lines.  The result is either that position or the beginning of its
# immediately preceding contiguous comment block.  Moving the anchor, rather
# than the comment, preserves comment text and indentation byte-for-byte.
def get_spacing_anchor(lines: list[str], statement_index: int) -> int:
    anchor = statement_index

    # Walk backward across only uninterrupted comment-leading lines.  A blank
    # line or ordinary code line establishes a hard ownership boundary.
    while anchor > 0 and is_comment_line(lines[anchor - 1]):
        anchor -= 1
    return anchor


# Tests whether a blank separator is required at an already resolved anchor.
# ``anchor`` must be a valid insertion index in ``lines``.  The result is false
# at the start of a file, after existing whitespace, or directly after an
# opening brace; otherwise it is true.  This makes the operation idempotent and
# implements the exception for the first statement in a block.
def is_blank_line_required(lines: list[str], anchor: int) -> bool:
    if anchor == 0 or not lines[anchor - 1].strip():
        return False

    # ``clang-format`` may put a brace after a declaration or control header,
    # so test the final non-whitespace character rather than requiring a line
    # containing only the brace.
    return not lines[anchor - 1].rstrip().endswith("{")


# Applies all bespoke rules to one complete C source string.  Input is expected
# to be the output of ``clang-format``.  Transformations run in their declared
# order for every line, and the result preserves whether the input had a final
# newline.  The function has no file-system effects, which keeps rule behavior
# easy to unit test.
def format_source(source: str) -> str:
    # ``splitlines`` normalizes line handling and omits terminators.  Record the
    # conventional final newline separately so formatting does not add or
    # remove one as an unrelated side effect.
    had_final_newline = source.endswith("\n")
    lines = source.splitlines()
    formatted: list[str] = []

    for line in lines:
        # Remove the discarded-result cast before structural spacing.  This
        # rule changes no line count, so subsequent insertion indexes remain
        # local to the ``formatted`` output list.
        line = DISCARDED_CALL_CAST.sub(r"\g<indent>", line)

        # Resolve spacing against output accumulated so far.  Doing this during
        # construction avoids index drift when multiple blank lines are added
        # to the same file.
        if CONTROL_BLOCK.match(line):
            anchor = get_spacing_anchor(formatted, len(formatted))
            if is_blank_line_required(formatted, anchor):
                formatted.insert(anchor, "")
        formatted.append(line)

    # Reconstruct with LF separators.  The format task targets repository text
    # files and intentionally produces stable cross-platform line endings.
    result = "\n".join(formatted)
    return result + "\n" if had_final_newline else result


# Formats every command-line source path in place.  Arguments must name UTF-8
# text files and are processed in their supplied order.  Unchanged files are
# not rewritten, preserving timestamps and avoiding unnecessary editor/build
# churn.  Observable read, decode, or write failures propagate to produce a
# nonzero process exit; successful completion returns zero.
def main(arguments: list[str]) -> int:
    for argument in arguments:
        path = Path(argument)
        source = path.read_text(encoding="utf-8")
        formatted = format_source(source)

        # Avoid touching a file when all bespoke rules are already satisfied.
        if formatted != source:
            path.write_text(formatted, encoding="utf-8", newline="\n")
    return 0


# Convert the explicit return contract into the process status expected by the
# PowerShell and shell format tasks.  Importing this module for unit tests does
# not execute the command-line entry point.
if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
