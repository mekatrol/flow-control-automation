# Repository coding rules

These rules apply to all code in the 'controllers' directory in this repository.
Agents must ensure that every code change, including new code and modifications
to existing code, follows all rules in this file and the formatting rules in
`.clang-format`. Do not introduce or preserve noncompliant code in a file being
changed.

Developers and agents working from a clean environment must follow
[`SETUP_DEV.md`](SETUP_DEV.md) for toolchain, board-selection, build, test, and
formatting setup.

1. Every method or function declaration must have a preceding comment that
   describes its contract: what it does, its relevant preconditions, and its
   result or observable effects. Parameters and local variables do not need
   individual comments when their names make their purpose unambiguous.

   Public header contracts must use Doxygen hover documentation. Document every
   public enum, struct, typedef, callback, function, and each enum value or
   struct field. Function and callback comments must include `@param` entries
   for every parameter and `@return` when non-void. State ownership, lifetime,
   nullability, units, inclusive or exclusive numeric bounds, buffer capacity
   rules, accepted enum/string vocabulary, and observable failure behavior.
   Comments that only repeat an identifier or C type are not compliant. Private
   translation-unit contract types require the same semantic field and range
   documentation, although ordinary private helpers may use non-Doxygen
   contract comments.

2. Comment logic inside method and function bodies with both what the logic
   does and why it is necessary. Do not add comments that merely restate
   self-explanatory syntax.

3. Do not use magic numbers. Give meaningful numeric values named constants or
   enum members, and document their purpose. Literal values that are intrinsic
   to an operation, such as zero initialization or incrementing by one, may be
   used directly when their meaning is unambiguous.

4. Do not use magic strings. Give meaningful string values named constants and
   document their purpose. Literal strings used only as user-facing prose or
   diagnostic formatting may remain inline when extracting them would reduce
   clarity.

5. Name methods and functions that test a condition and return a Boolean with
   an `is_` prefix.

6. Name methods and functions whose primary purpose is retrieving a value with
   a `get_` prefix.

7. Separate code by feature or responsibility. Do not place unrelated
   functionality in the same source file. Constants, types, and enums may be
   grouped when they form a coherent shared contract.

8. Put a blank line before each control-flow statement that opens a block and
   before each `return` statement, unless it is the first statement after an
   opening brace. When a comment immediately documents the statement, put the
   blank line before the comment instead. The repository format task enforces
   this rule. Put a blank line after each closing brace unless it is followed
   by another closing brace. Put a blank line before a comment block that
   follows another statement, unless it is the first item after an opening
   brace.

9. Do not cast a discarded function-call result to `(void)`. Call the function
   directly when its result is intentionally ignored. The repository format
   task removes these casts.
