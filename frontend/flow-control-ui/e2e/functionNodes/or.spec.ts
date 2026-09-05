import { booleanBinaryCase, defineFunctionNodeTest, test } from './helpers/functionNodeCase';

test(...defineFunctionNodeTest(booleanBinaryCase('or', [false, true, true, true])));
