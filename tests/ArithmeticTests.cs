namespace Exel.Tests;
public class ArithmeticTests
{
    private static CellResult Evaluate(string text) { var b = new Workbook(); b.Set("A1", text); return new WorkbookCalculator(b).Calculate()["A1"]; }
    [Theory]
    [InlineData("2+3*4", "14")]
    [InlineData("(2+3)*4", "20")]
    [InlineData("20-7-3", "10")]
    [InlineData("24/4/2", "3")]
    [InlineData("17div5", "3")]
    [InlineData("17mod5", "2")]
    [InlineData("(0-17)/5", "-3")]
    [InlineData("(0-17)div5", "-3")]
    [InlineData("(0-17)mod5", "-2")]
    [InlineData("17mod(0-5)", "2")]
    [InlineData("2^3^2", "512")]
    [InlineData("(2^3)^2", "64")]
    [InlineData("0^0", "1")]
    [InlineData("1^100000", "1")]
    [InlineData("max(7,min(20,4))", "7")]
    [InlineData("MiN(9,4)+MAX(3,8)", "12")]
    [InlineData("  = 00012+3 ", "15")]
    [InlineData("12DIV5MOD2", "0")]
    public void ExpressionsMatchIndependentExpectedValues(string input, string expected)
    { var r = Evaluate(input); Assert.Null(r.Error); Assert.Equal(BigInteger.Parse(expected), r.Value); }
    [Theory]
    [InlineData("1/0", "#DIV0")]
    [InlineData("1mod0", "#DIV0")]
    [InlineData("2^(0-1)", "#DOMAIN")]
    [InlineData("2^100001", "#LIMIT")]
    [InlineData("2^65536", "#LIMIT")]
    [InlineData("CW1", "#REF")]
    public void CalculationErrorsRemainDistinct(string input, string code) => Assert.Equal(code, Evaluate(input).Error?.Code);
}
