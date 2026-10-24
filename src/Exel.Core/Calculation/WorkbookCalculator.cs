using System.Numerics;
namespace Exel.Core;

public sealed record CellResult(BigInteger? Value, FormulaException? Error)
{
    // Format once on the calculation worker, never repeatedly while painting the grid.
    public string Display { get; } = Error?.Code ?? Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
}
public sealed class WorkbookCalculator
{
    private readonly Workbook workbook;
    private readonly Dictionary<string, Expression> syntax = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CellResult> results = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> visiting = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationToken cancellation;
    private int operations, evaluationDepth;
    private const int MaxBits = 65_536;
    public WorkbookCalculator(Workbook workbook, CancellationToken cancellation = default)
    {
        this.workbook = workbook.Snapshot(); this.cancellation = cancellation;
        // Phase 1: parse every filled cell before evaluating any cell.
        foreach (var (address, formula) in this.workbook.Cells)
        {
            cancellation.ThrowIfCancellationRequested();
            try { syntax[address] = new FormulaParser().Parse(formula); }
            catch (FormulaException ex) { results[address] = new(null, ex); }
        }
    }
    public Dictionary<string, CellResult> Calculate()
    {
        foreach (var address in workbook.Cells.Keys) _ = Cell(address);
        return new(results, StringComparer.OrdinalIgnoreCase);
    }
    private CellResult Cell(string address)
    {
        cancellation.ThrowIfCancellationRequested();
        if (results.TryGetValue(address, out var cached)) return cached;
        if (visiting.Contains(address)) return new(null, new("#CYCLE", $"Циклічне посилання через {address}."));
        if (visiting.Count >= 128) return new(null, new("#LIMIT", "Ланцюжок посилань перевищує 128 клітинок."));
        if (!syntax.TryGetValue(address, out var node)) return new(BigInteger.Zero, null);
        visiting.Add(address);
        try { results[address] = new(Evaluate(node), null); }
        catch (FormulaException ex) { results[address] = new(null, ex); }
        finally { visiting.Remove(address); }
        return results[address];
    }
    private BigInteger Evaluate(Expression node)
    {
        if (evaluationDepth >= 256) throw new FormulaException("#LIMIT", "Сумарна глибина обчислень перевищує 256.", node.Position);
        evaluationDepth++;
        try { return EvaluateCore(node); }
        finally { evaluationDepth--; }
    }
    private BigInteger EvaluateCore(Expression node)
    {
        cancellation.ThrowIfCancellationRequested();
        if (++operations > 1_000_000) throw new FormulaException("#LIMIT", "Перевищено бюджет обчислень.", node.Position);
        BigInteger value;
        switch (node)
        {
            case NumberExpression n: value = n.Value; break;
            case CellExpression c:
                if (!CellAddress.InBounds(c.Address, workbook.Rows, workbook.Columns))
                    throw new FormulaException("#REF", $"Адреса {c.Address} поза межами таблиці.", c.Position, c.Address.Length);
                var dependency = Cell(c.Address);
                if (dependency.Error is { } error) throw new FormulaException(error.Code, $"Посилання {c.Address}: {error.Message}", c.Position, c.Address.Length);
                value = dependency.Value ?? BigInteger.Zero; break;
            case FunctionExpression f:
                var x = Evaluate(f.Left); var y = Evaluate(f.Right);
                value = f.Name == "MAX" ? BigInteger.Max(x, y) : BigInteger.Min(x, y); break;
            case BinaryExpression b:
                var left = Evaluate(b.Left); var right = Evaluate(b.Right);
                if ((b.Operator is "/" or "DIV" or "MOD") && right.IsZero) throw new FormulaException("#DIV0", "Ділення на нуль.", b.Position);
                if (b.Operator == "^")
                {
                    if (right < 0) throw new FormulaException("#DOMAIN", "Показник степеня має бути невід’ємним цілим.", b.Right.Position);
                    if (right > 100_000) throw new FormulaException("#LIMIT", "Показник степеня перевищує 100000.", b.Right.Position);
                    value = Pow(left, (int)right, b.Position);
                }
                else value = b.Operator switch
                {
                    "+" => left + right, "-" => left - right, "*" => left * right,
                    "/" or "DIV" => left / right, "MOD" => left % right,
                    _ => throw new InvalidOperationException()
                };
                break;
            default: throw new InvalidOperationException();
        }
        return Checked(value, node.Position);
    }
    private static BigInteger Checked(BigInteger value, int position)
    {
        if (value.GetBitLength() > MaxBits) throw new FormulaException("#LIMIT", "Результат перевищує ліміт 65536 біт.", position);
        return value;
    }
    private BigInteger Pow(BigInteger value, int exponent, int position)
    {
        BigInteger result = 1;
        while (exponent > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            if ((exponent & 1) != 0) result = Checked(result * value, position);
            exponent >>= 1;
            if (exponent > 0) value = Checked(value * value, position);
        }
        return result;
    }
}

