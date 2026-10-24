namespace Exel.Core;

public static class SyntaxValidator
{
    public static Dictionary<string, FormulaException> Validate(Workbook book, CancellationToken cancellation = default)
    {
        var errors = new Dictionary<string, FormulaException>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, text) in book.Cells)
        {
            cancellation.ThrowIfCancellationRequested();
            try { new FormulaParser().Parse(text); }
            catch (FormulaException ex) { errors[key] = ex; }
        }
        return errors;
    }
}
