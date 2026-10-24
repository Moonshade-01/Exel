namespace Exel.Core;

public static class CellAddress
{
    public static bool IsValid(string value)
    {
        int i = 0;
        while (i < value.Length && value[i] is >= 'A' and <= 'Z') i++;
        if (i == 0 || i > 2 || i == value.Length || value[i] == '0') return false;
        return value[i..].All(char.IsAsciiDigit) && int.TryParse(value[i..], out var row) && row > 0;
    }
    public static string Column(int index)
    {
        string result = "";
        for (index++; index > 0; index = (index - 1) / 26) result = (char)('A' + (index - 1) % 26) + result;
        return result;
    }
    public static bool InBounds(string address, int rows, int columns)
    {
        if (!IsValid(address)) return false;
        int col = 0, i = 0;
        while (i < address.Length && char.IsAsciiLetter(address[i])) col = col * 26 + address[i++] - 'A' + 1;
        return col <= columns && int.Parse(address[i..]) <= rows;
    }
}
