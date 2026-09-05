import { booleanBinaryCase, defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest(booleanBinaryCase('nor', [true, false, false, false])));
