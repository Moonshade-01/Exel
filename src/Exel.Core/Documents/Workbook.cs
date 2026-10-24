namespace Exel.Core;

public sealed class Workbook
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Нова таблиця";
    public int Rows { get; set; } = 100;
    public int Columns { get; set; } = 26;
    public Dictionary<string, string> Cells { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Workbook Snapshot() => new() { Name = Name, Rows = Rows, Columns = Columns, Cells = new(Cells, StringComparer.OrdinalIgnoreCase) };
    public string Get(string address) => Cells.GetValueOrDefault(address, "");
    public void Set(string address, string formula)
    {
        address = address.ToUpperInvariant();
        if (!CellAddress.InBounds(address, Rows, Columns)) throw new ArgumentException("Клітинка поза межами таблиці.");
        if (formula.Length > FormulaParser.MaxFormulaLength) throw new ArgumentException("Вираз перевищує 4096 символів.");
        if (string.IsNullOrWhiteSpace(formula)) Cells.Remove(address); else Cells[address] = formula;
    }
}
