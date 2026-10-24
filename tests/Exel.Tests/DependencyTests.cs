namespace Exel.Tests;

public class DependencyTests
{
    [Fact]
    public void ReferencesAreCaseInsensitiveAndBlankCellsAreZero()
    {
        var b = new Workbook(); b.Set("a1","10"); b.Set("B1","=a1*2+C2");
        Assert.Equal(new BigInteger(20),new WorkbookCalculator(b).Calculate()["B1"].Value);
    }
    [Fact]
    public void SharedDependencyIsNotACycle()
    {
        var b = new Workbook(); b.Set("A1","7");b.Set("B1","A1*2");b.Set("C1","A1*3");b.Set("D1","B1+C1");
        var r=new WorkbookCalculator(b).Calculate();Assert.All(r.Values,v=>Assert.Null(v.Error));Assert.Equal(new BigInteger(35),r["D1"].Value);
    }
    [Theory][InlineData("A1")][InlineData("=A1")][InlineData("a1+1")]
    public void DirectCycleIsReported(string expression)
    {
        var b=new Workbook();b.Set("A1",expression);Assert.Equal("#CYCLE",new WorkbookCalculator(b).Calculate()["A1"].Error?.Code);
    }
    [Fact]
    public void IndirectCyclePropagatesAndRecoversAfterBreakingIt()
    {
        var b=new Workbook();b.Set("A1","B1");b.Set("B1","C1");b.Set("C1","A1");b.Set("D1","B1+1");b.Set("E1","2^10");
        var r=new WorkbookCalculator(b).Calculate();foreach(var key in new[]{"A1","B1","C1","D1"})Assert.Equal("#CYCLE",r[key].Error?.Code);
        Assert.Equal(new BigInteger(1024),r["E1"].Value);b.Set("A1","7");r=new WorkbookCalculator(b).Calculate();
        Assert.All(r.Values,v=>Assert.Null(v.Error));Assert.Equal(new BigInteger(8),r["D1"].Value);
    }
    [Fact]
    public void DependencyErrorPointsToCurrentReference()
    {
        var b=new Workbook();b.Set("A1","1/0");b.Set("B1","2+A1");var e=new WorkbookCalculator(b).Calculate()["B1"].Error;
        Assert.NotNull(e);Assert.Equal("#DIV0",e.Code);Assert.Equal(2,e.Position);Assert.Equal(2,e.Length);
    }
    [Fact]
    public void CalculatorUsesStableSnapshot()
    {
        var b=new Workbook();b.Set("A1","1");var calculator=new WorkbookCalculator(b);b.Set("A1","99");
        Assert.Equal(BigInteger.One,calculator.Calculate()["A1"].Value);Assert.Equal(new BigInteger(99),new WorkbookCalculator(b).Calculate()["A1"].Value);
    }
    [Fact]
    public void OutOfBoundsReferenceIsNotSilentlyTreatedAsBlank()
    {
        var b=new Workbook{Rows=1,Columns=1};b.Set("A1","B1");Assert.Equal("#REF",new WorkbookCalculator(b).Calculate()["A1"].Error?.Code);
    }
    [Fact]
    public void LongDependencyChainReturnsLimitInsteadOfOverflow()
    {
        var b=new Workbook{Rows=200};for(int i=1;i<150;i++)b.Set("A"+i,"A"+(i+1));b.Set("A150","1");
        Assert.Equal("#LIMIT",new WorkbookCalculator(b).Calculate()["A1"].Error?.Code);
    }
    [Fact]
    public void CancelledWorkDoesNotContinueParsing()
    {
        var b=new Workbook();b.Set("A1","1");using var c=new CancellationTokenSource();c.Cancel();
        Assert.Throws<OperationCanceledException>(()=>new WorkbookCalculator(b,c.Token));
        Assert.Throws<OperationCanceledException>(()=>SyntaxValidator.Validate(b,c.Token));
    }
}
