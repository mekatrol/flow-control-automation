import { booleanBinaryCase, defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest(booleanBinaryCase('sequence', [false, false, false, true])));
