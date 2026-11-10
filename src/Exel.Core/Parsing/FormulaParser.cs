using Antlr4.Runtime;
using Exel.Core.Parsing.Antlr;
namespace Exel.Core;

// ANTLR performs syntax analysis; the visitor builds an AST without evaluating it.
public sealed class FormulaParser
{
    public const int MaxFormulaLength = 4096;
    public Expression Parse(string text)
    {
        if (text.Length > MaxFormulaLength)
            throw new FormulaException("#LIMIT", "Вираз перевищує 4096 символів.");
        var errors = new FormulaErrorListener(text);
        var lexer = new FormulaGrammarLexer(new AntlrInputStream(text));
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(errors);
        var tokens = new CommonTokenStream(lexer);
        // Lex the entire input first, so invalid characters cannot be silently skipped.
        tokens.Fill();
        var parser = new FormulaGrammarParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errors);
        parser.AddParseListener(new FormulaNestingListener());
        var tree = parser.formula();
        return new FormulaAstVisitor().Visit(tree);
    }
}
