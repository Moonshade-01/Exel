using Antlr4.Runtime;
namespace Exel.Core;

// Throw at the first lexer/parser error instead of accepting ANTLR recovery output.
internal sealed class FormulaErrorListener(string source) : BaseErrorListener, IAntlrErrorListener<int>
{
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
        int line, int charPositionInLine, string msg, RecognitionException e)
    {
        int position = Math.Clamp(offendingSymbol.StartIndex, 0, source.Length);
        string message = offendingSymbol.Type == TokenConstants.EOF
            ? "Незавершений вираз: очікується операнд або закривальна дужка."
            : $"Неочікувана лексема «{offendingSymbol.Text}». Перевірте операції, дужки й аргументи функцій. Унарні знаки не входять до варіанта 6.";
        throw new FormulaException("#SYNTAX", message, position, Math.Max(1, offendingSymbol.StopIndex - position + 1));
    }
    public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
        int line, int charPositionInLine, string msg, RecognitionException e)
    {
        int position = e is LexerNoViableAltException lexical ? lexical.StartIndex : ((Lexer)recognizer).InputStream.Index;
        position = Math.Clamp(position, 0, source.Length);
        string message = position < source.Length ? $"Невідомий символ «{source[position]}»." : "Некоректний кінець виразу.";
        throw new FormulaException("#SYNTAX", message, position);
    }
}
