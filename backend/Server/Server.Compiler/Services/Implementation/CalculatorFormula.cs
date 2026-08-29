namespace Server.Compiler.Services.Implementation;

internal static class CalculatorFormula
{
    internal enum Operator { Add, Subtract, Multiply, Divide, Power, Negate }
    internal abstract record Expression
    {
        internal sealed record Variable(char Name) : Expression;
        internal sealed record Unary(Operator Operator, Expression Operand) : Expression;
        internal sealed record Binary(Operator Operator, Expression Left, Expression Right) : Expression;
    }
    public static Expression Parse(string formula)
    {
        var parser = new Parser(formula);
        var result = parser.ParseExpression();
        parser.SkipWhitespace();
        if (!parser.AtEnd)
        {
            throw new FormatException($"Unexpected character '{parser.Current}'.");
        }

        return result;
    }

    public static int OperationCount(Expression expression) => expression switch
    {
        Expression.Variable => 0,
        Expression.Unary unary => 1 + OperationCount(unary.Operand),
        Expression.Binary binary => 1 + OperationCount(binary.Left) + OperationCount(binary.Right),
        _ => throw new InvalidOperationException()
    };

    public static HashSet<char> Variables(Expression expression)
    {
        var result = new HashSet<char>();
        Visit(expression);
        return result;

        void Visit(Expression item)
        {
            switch (item)
            {
                case Expression.Variable variable: result.Add(variable.Name); break;
                case Expression.Unary unary: Visit(unary.Operand); break;
                case Expression.Binary binary: Visit(binary.Left); Visit(binary.Right); break;
            }
        }
    }

    private sealed class Parser(string text)
    {
        private int _position;
        public bool AtEnd => _position >= text.Length;
        public char Current => AtEnd ? '\0' : text[_position];
        public void SkipWhitespace()
        {
            while (!AtEnd && char.IsWhiteSpace(Current))
            {
                _position++;
            }
        }

        public Expression ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (!Take('+') && !Take('-'))
                {
                    return value;
                }

                var operation = text[_position - 1] == '+' ? Operator.Add : Operator.Subtract;
                value = new Expression.Binary(operation, value, ParseTerm());
            }
        }

        private Expression ParseTerm()
        {
            var value = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (!Take('*') && !Take('/'))
                {
                    return value;
                }

                var operation = text[_position - 1] == '*' ? Operator.Multiply : Operator.Divide;
                value = new Expression.Binary(operation, value, ParseUnary());
            }
        }

        private Expression ParseUnary()
        {
            SkipWhitespace();
            if (Take('+'))
            {
                return ParseUnary();
            }

            if (Take('-'))
            {
                return new Expression.Unary(Operator.Negate, ParseUnary());
            }

            return ParsePower();
        }

        private Expression ParsePower()
        {
            var value = ParsePrimary();
            SkipWhitespace();
            return Take('^')
                ? new Expression.Binary(Operator.Power, value, ParseUnary())
                : value;
        }

        private Expression ParsePrimary()
        {
            SkipWhitespace();
            if (Take('('))
            {
                var value = ParseExpression();
                SkipWhitespace();
                if (!Take(')'))
                {
                    throw new FormatException("Missing closing parenthesis.");
                }

                return value;
            }

            if (!AtEnd && Current is 'a' or 'b' or 'c')
            {
                return new Expression.Variable(text[_position++]);
            }

            if (!AtEnd && (char.IsDigit(Current) || Current == '.'))
            {
                throw new FormatException("Numeric literals are not allowed; connect constants to a, b, or c.");
            }

            throw new FormatException(AtEnd ? "Expected a variable or parenthesized expression." : $"Unknown identifier or character '{Current}'.");
        }

        private bool Take(char value)
        {
            if (AtEnd || Current != value)
            {
                return false;
            }

            _position++;
            return true;
        }
    }
}