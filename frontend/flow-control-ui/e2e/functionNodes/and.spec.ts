import { booleanBinaryCase, defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest(booleanBinaryCase('and', [false, false, false, true])));
