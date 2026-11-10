namespace Exel.Tests;

public class AntlrFormulaTests
{
    [Fact]
    public void CompactReferencesAndFunctionsWorkThroughTheAntlrPipeline()
    {
        var book = new Workbook();
        book.Set("A1", "17"); book.Set("B2", "5"); book.Set("CV100", "7");
        book.Set("C1", "=mAx(a1DiVb2,cv100MoDb2)");
        var results = new WorkbookCalculator(book).Calculate();
        Assert.All(results.Values, result => Assert.Null(result.Error));
        Assert.Equal(new BigInteger(3), results["C1"].Value);
    }
    [Theory]
    [InlineData("2@+3", 1, 1)]
    [InlineData("2+3$", 3, 1)]
    [InlineData(" \n=2+\n*3", 6, 1)]
    [InlineData("max(1,2,3)", 7, 1)]
    [InlineData("1\n+CV100 2", 9, 1)]
    [InlineData("=2+", 3, 1)]
    [InlineData("A2147483648", 0, 11)]
    public void AntlrErrorsKeepOriginalSourceOffsets(string formula, int position, int length)
    {
        var error = Assert.Throws<FormulaException>(() => new FormulaParser().Parse(formula));
        Assert.Equal("#SYNTAX", error.Code);
        Assert.Equal(position, error.Position);
        Assert.Equal(length, error.Length);
    }
    [Theory]
    [InlineData("2^3^2")]
    [InlineData("2^ 3 ^2")]
    public void PowerVisitorPreservesRightAssociativity(string formula)
    {
        var root = Assert.IsType<BinaryExpression>(new FormulaParser().Parse(formula));
        Assert.Equal("^", root.Operator);
        Assert.Equal(new BigInteger(2), Assert.IsType<NumberExpression>(root.Left).Value);
        Assert.Equal("^", Assert.IsType<BinaryExpression>(root.Right).Operator);
    }
    [Theory]
    [InlineData("2+3 garbage")]
    [InlineData("2+3=4")]
    [InlineData("max(1,,2)")]
    [InlineData("max(,2)")]
    [InlineData("min(1,2)3")]
    [InlineData("(=2+3)")]
    [InlineData("1e2")]
    [InlineData("A 1")]
    public void AntlrNeverAcceptsRecoveredOrPartialExpressions(string formula) =>
        Assert.Equal("#SYNTAX", Assert.Throws<FormulaException>(() => new FormulaParser().Parse(formula)).Code);
    [Fact]
    public void UnicodeWhitespaceRemainsSupportedWithoutJoiningOperands()
    {
        var book = new Workbook(); book.Set("A1", "\u00A0=\u20032+\u202F3\u3000");
        Assert.Equal(new BigInteger(5), new WorkbookCalculator(book).Calculate()["A1"].Value);
        Assert.Throws<FormulaException>(() => new FormulaParser().Parse("1\u00A02"));
    }
    [Fact]
    public void RecursiveGrammarLimitsAreEnforcedBeforeStackExhaustion()
    {
        var parser = new FormulaParser();
        Assert.IsType<NumberExpression>(parser.Parse(new string('(', 127) + "1" + new string(')', 127)));
        Assert.Throws<FormulaException>(() => parser.Parse(new string('(', 128) + "1" + new string(')', 128)));
        Assert.IsType<BinaryExpression>(parser.Parse(string.Join("^", Enumerable.Repeat("1", 128))));
        Assert.Throws<FormulaException>(() => parser.Parse(string.Join("^", Enumerable.Repeat("1", 129))));
        string nested = new string('(', 63) + string.Join("^", Enumerable.Repeat("1", 65)) + new string(')', 63);
        Assert.IsType<BinaryExpression>(parser.Parse(nested));
        Assert.Throws<FormulaException>(() => parser.Parse("(" + nested + ")"));
        Assert.IsType<NumberExpression>(parser.Parse("42"));
    }
    [Fact]
    public void LongFlatFormulasParseButRespectEvaluationLimits()
    {
        string flat = string.Join("+", Enumerable.Repeat("1", 2000));
        Assert.IsType<BinaryExpression>(new FormulaParser().Parse(flat));
        var book = new Workbook(); book.Set("A1", flat);
        Assert.Equal("#LIMIT", new WorkbookCalculator(book).Calculate()["A1"].Error?.Code);
    }
}
