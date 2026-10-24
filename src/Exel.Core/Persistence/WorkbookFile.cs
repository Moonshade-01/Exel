using System.Text.Json;
namespace Exel.Core;

public static class WorkbookFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string Serialize(Workbook workbook)
    {
        string json = JsonSerializer.Serialize(workbook, Options);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 4_000_000) throw new InvalidDataException("Таблиця перевищує ліміт файлу 4 МБ.");
        return json;
    }
    public static Workbook Deserialize(string json)
    {
        if (json.Length > 4_000_000) throw new InvalidDataException("Файл перевищує 4 МБ.");
        var w = JsonSerializer.Deserialize<Workbook>(json, Options) ?? throw new InvalidDataException("Порожній документ.");
        if (w.Version != 1 || w.Rows is < 1 or > 200 || w.Columns is < 1 or > 52 || string.IsNullOrWhiteSpace(w.Name) || w.Name.Length > 120 || w.Cells is null)
            throw new InvalidDataException("Непідтримувана версія або некоректні параметри таблиці.");
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in w.Cells)
            if (!CellAddress.InBounds(key.ToUpperInvariant(), w.Rows, w.Columns) || value is null || value.Length > FormulaParser.MaxFormulaLength || !normalized.TryAdd(key.ToUpperInvariant(), value))
                throw new InvalidDataException($"Некоректні дані клітинки {key}.");
        w.Cells = normalized; return w;
    }
}
