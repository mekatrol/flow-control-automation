import { booleanBinaryCase, defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest(booleanBinaryCase('xor', [false, true, true, false])));
