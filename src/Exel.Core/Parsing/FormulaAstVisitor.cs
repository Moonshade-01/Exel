using System.Globalization;
using System.Numerics;
using Exel.Core.Parsing.Antlr;
namespace Exel.Core;

internal sealed class FormulaAstVisitor : FormulaGrammarBaseVisitor<Expression>
{
    public override Expression VisitFormula(FormulaGrammarParser.FormulaContext context) => Visit(context.value);
    public override Expression VisitSum(FormulaGrammarParser.SumContext context)
    {
        Expression result = Visit(context.first);
        for (int i = 0; i < context._operators.Count; i++)
        {
            var op = context._operators[i];
            result = new BinaryExpression(op.Text, result, Visit(context._remaining[i]), op.StartIndex);
        }
        return result;
    }
    public override Expression VisitProduct(FormulaGrammarParser.ProductContext context)
    {
        Expression result = Visit(context.first);
        for (int i = 0; i < context._operators.Count; i++)
        {
            var op = context._operators[i];
            result = new BinaryExpression(op.Text.ToUpperInvariant(), result, Visit(context._remaining[i]), op.StartIndex);
        }
        return result;
    }
    public override Expression VisitPower(FormulaGrammarParser.PowerContext context)
    {
        Expression left = Visit(context.left);
        return context.right is null ? left : new BinaryExpression("^", left, Visit(context.right), context.operatorToken.StartIndex);
    }
    public override Expression VisitIntegerLiteral(FormulaGrammarParser.IntegerLiteralContext context) =>
        new NumberExpression(BigInteger.Parse(context.value.Text, CultureInfo.InvariantCulture), context.value.StartIndex);
    public override Expression VisitCellReference(FormulaGrammarParser.CellReferenceContext context)
    {
        string address = context.address.Text.ToUpperInvariant();
        // The row must also fit Int32; bounds belong to the evaluation phase.
        if (!CellAddress.IsValid(address))
            throw new FormulaException("#SYNTAX", "Некоректна адреса клітинки.", context.address.StartIndex, address.Length);
        return new CellExpression(address, context.address.StartIndex);
    }
    public override Expression VisitParenthesized(FormulaGrammarParser.ParenthesizedContext context) => Visit(context.value);
    public override Expression VisitFunctionCall(FormulaGrammarParser.FunctionCallContext context) =>
        new FunctionExpression(context.name.Text.ToUpperInvariant(), Visit(context.left), Visit(context.right), context.name.StartIndex);
}
