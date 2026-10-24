using System.Globalization;
using System.Numerics;
namespace Exel.Core;

// Parsing builds an AST only; evaluation is a separate phase.
public sealed class FormulaParser
{
    private enum Kind { Number, Word, Symbol, End }
    private sealed record Token(Kind Kind, string Text, int Start);
    private readonly List<Token> tokens = [];
    private int index, depth;
    public const int MaxFormulaLength = 4096;
    public Expression Parse(string text)
    {
        tokens.Clear(); index = 0; depth = 0;
        if (text.Length > MaxFormulaLength) throw new FormulaException("#LIMIT", "Вираз перевищує 4096 символів.");
        int prefix = 0;
        while (prefix < text.Length && char.IsWhiteSpace(text[prefix])) prefix++;
        int firstToken = prefix < text.Length && text[prefix] == '=' ? prefix + 1 : 0;
        for (int i = firstToken; i < text.Length;)
        {
            if (char.IsWhiteSpace(text[i])) { i++; continue; }
            int start = i;
            if (char.IsAsciiDigit(text[i]))
            {
                while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
                tokens.Add(new(Kind.Number, text[start..i], start));
            }
            else if (char.IsAsciiLetter(text[i]))
            {
                // Reserved words can touch their operands: 17div5, A1modB2.
                string? keyword = null;
                foreach (string candidate in new[] { "DIV", "MOD", "MAX", "MIN" })
                    if (text.AsSpan(i).StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
                    { keyword = candidate; break; }
                if (keyword is not null)
                {
                    i += keyword.Length;
                    tokens.Add(new(Kind.Word, keyword, start));
                }
                else
                {
                    while (i < text.Length && char.IsAsciiLetter(text[i])) i++;
                    while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
                    tokens.Add(new(Kind.Word, text[start..i].ToUpperInvariant(), start));
                }
            }
            else if ("+-*/^(),".Contains(text[i])) tokens.Add(new(Kind.Symbol, text[i++].ToString(), start));
            else throw new FormulaException("#SYNTAX", $"Невідомий символ «{text[i]}».", i);
        }
        tokens.Add(new(Kind.End, "", text.Length));
        var result = Sum();
        if (Current.Kind != Kind.End) Fail("Очікується операція або кінець виразу.");
        return result;
    }
    private Token Current => tokens[index];
    private Token Take() => tokens[index++];
    private bool Is(string value) => Current.Text == value;
    private void Fail(string message) => throw new FormulaException("#SYNTAX", message, Current.Start, Math.Max(1, Current.Text.Length));
    private Expression Sum()
    {
        var left = Product();
        while (Is("+") || Is("-")) { var op = Take(); left = new BinaryExpression(op.Text, left, Product(), op.Start); }
        return left;
    }
    private Expression Product()
    {
        var left = Power();
        while (Is("*") || Is("/") || Is("DIV") || Is("MOD"))
        { var op = Take(); left = new BinaryExpression(op.Text, left, Power(), op.Start); }
        return left;
    }
    private Expression Power()
    {
        if (++depth > 128) Fail("Вкладеність виразу перевищує 128.");
        try
        {
            var left = Primary();
            if (!Is("^")) return left;
            var op = Take(); return new BinaryExpression("^", left, Power(), op.Start);
        }
        finally { depth--; }
    }
    private Expression Primary()
    {
        if (Current.Kind == Kind.Number)
        { var n = Take(); return new NumberExpression(BigInteger.Parse(n.Text, CultureInfo.InvariantCulture), n.Start); }
        if (Is("(")) { Take(); var result = Sum(); Require(")"); return result; }
        if (Is("MAX") || Is("MIN"))
        {
            var f = Take(); Require("("); var a = Sum(); Require(","); var b = Sum(); Require(")");
            return new FunctionExpression(f.Text, a, b, f.Start);
        }
        if (Current.Kind == Kind.Word && CellAddress.IsValid(Current.Text))
        { var c = Take(); return new CellExpression(c.Text, c.Start); }
        Fail("Очікується число, адреса клітинки, max/min або «(». Унарні знаки не входять до варіанта 6.");
        throw new InvalidOperationException();
    }
    private void Require(string value) { if (!Is(value)) Fail($"Очікується «{value}»."); Take(); }
}
