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
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 4_000_000) throw new InvalidDataException("Файл перевищує 4 МБ.");
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Документ таблиці має бути JSON-об’єктом.");
        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!properties.Add(property.Name)) throw new InvalidDataException("Повторене поле документа.");
            if (!property.Name.Equals("Cells", StringComparison.OrdinalIgnoreCase) || property.Value.ValueKind != JsonValueKind.Object) continue;
            var cellNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in property.Value.EnumerateObject())
                if (!cellNames.Add(cell.Name)) throw new InvalidDataException($"Повторена адреса клітинки {cell.Name}.");
        }
        var w = document.RootElement.Deserialize<Workbook>(Options) ?? throw new InvalidDataException("Порожній документ.");
        if (w.Version != 1 || w.Rows is < 1 or > 100 || w.Columns is < 1 or > 100 || string.IsNullOrWhiteSpace(w.Name) || w.Name.Length > 120 || w.Cells is null)
            throw new InvalidDataException("Непідтримувана версія або некоректні параметри таблиці.");
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in w.Cells)
        {
            string address = key.ToUpperInvariant();
            if (!CellAddress.InBounds(address, w.Rows, w.Columns) || value is null || value.Length > FormulaParser.MaxFormulaLength || !addresses.Add(address))
                throw new InvalidDataException($"Некоректні дані клітинки {key}.");
            if (!string.IsNullOrWhiteSpace(value)) normalized.Add(address, value);
        }
        w.Cells = normalized; return w;
    }
}
