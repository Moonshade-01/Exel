namespace Exel.Tests;

public class FormulaTests
{
    private static CellResult Evaluate(string formula)
    {
        var book = new Workbook(); book.Set("A1", formula);
        return new WorkbookCalculator(book).Calculate()["A1"];
    }
    [Theory]
    [InlineData("2+3*4", "14")]
    [InlineData("(2+3)*4", "20")]
    [InlineData("20-7-3", "10")]
    [InlineData("24/4/2", "3")]
    [InlineData("2^3^2", "512")]
    [InlineData("(2^3)^2", "64")]
    [InlineData("5^8", "390625")]
    [InlineData("17div5", "3")]
    [InlineData("17mod5", "2")]
    [InlineData("(0-17)/5", "-3")]
    [InlineData("(0-17)div5", "-3")]
    [InlineData("(0-17)mod5", "-2")]
    [InlineData("17mod(0-5)", "2")]
    [InlineData("max(7,min(20,4))", "7")]
    [InlineData("MiN(9,4)+MAX(3,8)", "12")]
    [InlineData("0^0", "1")]
    [InlineData("0^100000", "0")]
    [InlineData("1^100000", "1")]
    [InlineData("(0-2)^3", "-8")]
    [InlineData("(0-2)^4", "16")]
    [InlineData("=5^8", "390625")]
    [InlineData("  = 2 + 3 ", "5")]
    [InlineData("00012+3", "15")]
    [InlineData("12DIV5MOD2", "0")]
    public void ArithmeticMatchesKnownResults(string formula, string expected)
    {
        var result = Evaluate(formula); Assert.Null(result.Error);
        Assert.Equal(BigInteger.Parse(expected), result.Value);
    }
    [Fact]
    public void ArbitraryPrecisionExceedsMachineIntegers()
    {
        var result = Evaluate("2^1000+1");
        Assert.Null(result.Error); Assert.Equal(BigInteger.Pow(2, 1000) + 1, result.Value);
        Assert.Equal((BigInteger.Pow(2, 1000) + 1).ToString(), result.Display);
    }
    [Theory]
    [InlineData("1/0", "#DIV0")][InlineData("1div0", "#DIV0")][InlineData("1mod0", "#DIV0")]
    [InlineData("2^(0-1)", "#DOMAIN")][InlineData("2^100001", "#LIMIT")]
    [InlineData("2^65536", "#LIMIT")][InlineData("Z101", "#REF")]
    public void ArithmeticErrorsRemainDistinct(string formula, string code) => Assert.Equal(code, Evaluate(formula).Error?.Code);
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("=")][InlineData("==1")]
    [InlineData("2+*3")][InlineData("(2+3")][InlineData("2+3)")][InlineData("max(1)")]
    [InlineData("max(1,2,3)")][InlineData("foo(1,2)")][InlineData("1 2")][InlineData("A1B2")]
    [InlineData("12A1")][InlineData("-5")][InlineData("+5")][InlineData("1.5")]
    [InlineData("8divdiv2")][InlineData("А1")][InlineData("A01")][InlineData("2=3")]
    public void MalformedExpressionsAreRejected(string formula) => Assert.Equal("#SYNTAX", Assert.Throws<FormulaException>(() => new FormulaParser().Parse(formula)).Code);
    [Fact]
    public void OptionalPrefixPreservesSourcePosition()
    {
        var error = Assert.Throws<FormulaException>(() => new FormulaParser().Parse(" =2+*3"));
        Assert.Equal(4, error.Position); Assert.Equal(1, error.Length);
    }
    [Fact]
    public void SyntaxCheckDoesNotExecuteArithmetic()
    {
        var book = new Workbook(); book.Set("A1", "10div0"); book.Set("B1", "2^100001");
        Assert.Empty(SyntaxValidator.Validate(book));
        var results = new WorkbookCalculator(book).Calculate();
        Assert.Equal("#DIV0", results["A1"].Error?.Code); Assert.Equal("#LIMIT", results["B1"].Error?.Code);
    }
    [Fact]
    public void ParserCanRecoverAfterInvalidInput()
    {
        var parser = new FormulaParser(); Assert.Throws<FormulaException>(() => parser.Parse("max("));
        Assert.IsType<NumberExpression>(parser.Parse("42"));
    }
    [Fact]
    public void FormulaLengthAndNestingAreBounded()
    {
        Assert.Equal("#LIMIT", Assert.Throws<FormulaException>(() => new FormulaParser().Parse(new string('1',4097))).Code);
        Assert.Throws<FormulaException>(() => new FormulaParser().Parse(new string('(',129)+"1"+new string(')',129)));
    }
}
