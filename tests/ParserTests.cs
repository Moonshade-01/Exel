namespace Exel.Tests;
public class ParserTests
{
    [Theory]
    [InlineData("")][InlineData("=")][InlineData("==1")][InlineData("2+*3")]
    [InlineData("(2+3")][InlineData("max(1)")][InlineData("min(1,2,3)")]
    [InlineData("1 2")][InlineData("A1B2")][InlineData("12A1")]
    [InlineData("-5")][InlineData("+5")][InlineData("1.5")][InlineData("А1")][InlineData("2=3")]
    public void MalformedInputIsRejected(string input) => Assert.Equal("#SYNTAX", Assert.Throws<FormulaException>(() => new FormulaParser().Parse(input)).Code);
    [Fact] public void ErrorsKeepOffsetsAcrossNewlines()
    { var e = Assert.Throws<FormulaException>(() => new FormulaParser().Parse(" \n=2+\n*3")); Assert.Equal(6, e.Position); Assert.Equal(1, e.Length); }
    [Fact] public void InvalidCharactersCannotBeSilentlySkipped()
    { var e = Assert.Throws<FormulaException>(() => new FormulaParser().Parse("2@+3")); Assert.Equal(1, e.Position); }
    [Fact] public void UnicodeWhitespaceDoesNotJoinOperands()
    { Assert.IsType<BinaryExpression>(new FormulaParser().Parse("\u00A0=2\u2003+3")); Assert.Throws<FormulaException>(() => new FormulaParser().Parse("1\u00A02")); }
    [Fact] public void LengthAndNestingLimitsProtectTheParser()
    { var p = new FormulaParser(); Assert.Equal("#LIMIT", Assert.Throws<FormulaException>(() => p.Parse(new string('1',4097))).Code); Assert.Throws<FormulaException>(() => p.Parse(new string('(',128)+"1"+new string(')',128))); Assert.IsType<NumberExpression>(p.Parse("42")); }
    [Fact] public void MaximumPowerNestingAndLongFlatInputsAreHandled()
    { var p = new FormulaParser(); Assert.IsType<BinaryExpression>(p.Parse(string.Join("^",Enumerable.Repeat("1",128)))); Assert.Throws<FormulaException>(() => p.Parse(string.Join("^",Enumerable.Repeat("1",129)))); Assert.IsType<BinaryExpression>(p.Parse(string.Join("+",Enumerable.Repeat("1",2000)))); }
}
