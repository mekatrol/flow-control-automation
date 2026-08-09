"""Tests for repository-specific source formatting rules."""

import importlib.util
import unittest
from pathlib import Path


FORMATTER_PATH = Path(__file__).parents[1] / "scripts" / "format-source.py"
FORMATTER_SPEC = importlib.util.spec_from_file_location("format_source", FORMATTER_PATH)
FORMATTER = importlib.util.module_from_spec(FORMATTER_SPEC)
assert FORMATTER_SPEC.loader is not None
FORMATTER_SPEC.loader.exec_module(FORMATTER)


class FormatSourceTests(unittest.TestCase):
    """Verifies custom formatting without requiring clang-format in the test environment."""

    # Ensures adjacent control blocks are separated while a block's first statement is not.
    def test_adds_blank_lines_between_statement_blocks(self) -> None:
        source = "void run(void)\n{\n    if (ready)\n    {\n        act();\n    }\n    for (;;)\n    {\n    }\n}\n"
        expected = "void run(void)\n{\n    if (ready)\n    {\n        act();\n    }\n\n    for (;;)\n    {\n    }\n}\n"
        self.assertEqual(FORMATTER.format_source(source), expected)

    # Ensures statement documentation remains attached and receives the separating blank line.
    def test_places_blank_line_before_preceding_comment(self) -> None:
        source = "    work();\n    /* Explain why the branch is needed. */\n    if (ready)\n"
        expected = "    work();\n\n    /* Explain why the branch is needed. */\n    if (ready)\n"
        self.assertEqual(FORMATTER.format_source(source), expected)

    # Ensures ignored call results use ordinary expression statements.
    def test_removes_discarded_function_call_cast(self) -> None:
        source = "    (void)get_u32(&reader, &value);\n"
        self.assertEqual(FORMATTER.format_source(source), "    get_u32(&reader, &value);\n")

    # Ensures casts used in expressions or on non-call values are not changed.
    def test_preserves_other_void_casts(self) -> None:
        source = "    callback((void *)context);\n    (void)value;\n"
        self.assertEqual(FORMATTER.format_source(source), source)


if __name__ == "__main__":
    unittest.main()
