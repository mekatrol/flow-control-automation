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

    # Ensures a return and its documentation are separated from preceding work.
    def test_adds_blank_line_before_documented_return(self) -> None:
        source = "    work();\n    /* Return whether a usable IPv4 address was found. */\n    return dns.ip.type == IPV4 && dns.ip.address != 0;\n"
        expected = "    work();\n\n    /* Return whether a usable IPv4 address was found. */\n    return dns.ip.type == IPV4 && dns.ip.address != 0;\n"
        self.assertEqual(FORMATTER.format_source(source), expected)

    # Ensures a return used as a block's first statement stays next to its opening brace.
    def test_does_not_separate_first_return_after_opening_brace(self) -> None:
        source = "int get_value(void)\n{\n    return VALUE;\n}\n"
        self.assertEqual(FORMATTER.format_source(source), source)

    # Ensures ordinary statements are separated from a preceding completed block.
    def test_adds_blank_line_after_closing_brace(self) -> None:
        source = "    if (ready)\n    {\n        act();\n    }\n    const uint64_t completed_us = get_time_us();\n"
        expected = "    if (ready)\n    {\n        act();\n    }\n\n    const uint64_t completed_us = get_time_us();\n"
        self.assertEqual(FORMATTER.format_source(source), expected)

    # Ensures nested blocks may close on adjacent lines without added whitespace.
    def test_does_not_separate_consecutive_closing_braces(self) -> None:
        source = "        act();\n    }\n}\n"
        self.assertEqual(FORMATTER.format_source(source), source)

    # Ensures an existing separator remains stable across repeated formatting.
    def test_closing_brace_spacing_is_idempotent(self) -> None:
        source = "    }\n\n    continue_work();\n"
        formatted = FORMATTER.format_source(source)
        self.assertEqual(formatted, source)
        self.assertEqual(FORMATTER.format_source(formatted), formatted)

    # Ensures documentation for a following statement is separated from prior work.
    def test_adds_blank_line_before_comment_after_statement(self) -> None:
        source = "    update_schedule();\n    /* Explain why publication is bounded. */\n    const bool is_publish_due = is_due();\n"
        expected = "    update_schedule();\n\n    /* Explain why publication is bounded. */\n    const bool is_publish_due = is_due();\n"
        self.assertEqual(FORMATTER.format_source(source), expected)

    # Ensures a comment at the start of a block remains next to its opening brace.
    def test_does_not_separate_first_comment_after_opening_brace(self) -> None:
        source = "void run(void)\n{\n    /* Explain the initial operation. */\n    initialize();\n}\n"
        self.assertEqual(FORMATTER.format_source(source), source)

    # Ensures lines belonging to one comment block are not separated.
    def test_does_not_separate_contiguous_comment_lines(self) -> None:
        source = "    work();\n    /* Explain the next operation.\n     * Preserve this continuation line.\n     */\n    continue_work();\n"
        expected = "    work();\n\n    /* Explain the next operation.\n     * Preserve this continuation line.\n     */\n    continue_work();\n"
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
