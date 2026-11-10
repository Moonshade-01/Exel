using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Exel.Core.Parsing.Antlr;
namespace Exel.Core;

// Guard recursive grammar entry before deeply nested input can exhaust the stack.
internal sealed class FormulaNestingListener : IParseTreeListener
{
    private int depth;
    public void EnterEveryRule(ParserRuleContext context)
    {
        if (context.RuleIndex == FormulaGrammarParser.RULE_power && ++depth > 128)
            throw new FormulaException("#SYNTAX", "Вкладеність виразу перевищує 128.", context.Start.StartIndex);
    }
    public void ExitEveryRule(ParserRuleContext context)
    {
        if (context.RuleIndex == FormulaGrammarParser.RULE_power) depth--;
    }
    public void VisitTerminal(ITerminalNode node) { }
    public void VisitErrorNode(IErrorNode node) { }
}
