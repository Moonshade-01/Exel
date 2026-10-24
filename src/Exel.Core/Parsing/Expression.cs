using System.Numerics;
namespace Exel.Core;

public sealed class FormulaException(string code, string message, int position = 0, int length = 1) : Exception(message)
{
    public string Code { get; } = code;
    public int Position { get; } = position;
    public int Length { get; } = length;
}
public abstract record Expression(int Position);
public sealed record NumberExpression(BigInteger Value, int Start) : Expression(Start);
public sealed record CellExpression(string Address, int Start) : Expression(Start);
public sealed record BinaryExpression(string Operator, Expression Left, Expression Right, int Start) : Expression(Start);
public sealed record FunctionExpression(string Name, Expression Left, Expression Right, int Start) : Expression(Start);

