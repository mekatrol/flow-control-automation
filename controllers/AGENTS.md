# Repository coding rules

These rules apply to all code in the 'controllers' directory in this repository.

1. Every method or function declaration must have a preceding comment that
   describes its contract: what it does, its relevant preconditions, and its
   result or observable effects. Parameters and local variables do not need
   individual comments when their names make their purpose unambiguous.

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
