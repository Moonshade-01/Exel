namespace Exel.Tests;

public class SpreadsheetTests
{
    [Fact]
    public void VariantSixOperationsRespectPrecedenceAndSupportBigInteger()
    {
        var book = new Workbook();
        book.Set("A1", "2^100+24/3*2-5+(17div5)*10+17mod5+max(7,min(20,4))");
        book.Set("B1", "2^3^2");
        book.Set("C1", "(0-17)div5*10+(0-17)mod5");

        var results = new WorkbookCalculator(book).Calculate();

        Assert.All(results.Values, result => Assert.Null(result.Error));
        Assert.Equal(BigInteger.Pow(2, 100) + 50, results["A1"].Value);
        Assert.Equal(new BigInteger(512), results["B1"].Value);
        Assert.Equal(new BigInteger(-32), results["C1"].Value);
    }

    [Fact]
    public void SyntaxCheckLocatesErrorsWithoutExecutingArithmetic()
    {
        var book = new Workbook();
        book.Set("A1", "=2+*3");
        book.Set("B1", "10div0");

        var errors = SyntaxValidator.Validate(book);

        Assert.Single(errors);
        Assert.Equal("#SYNTAX", errors["A1"].Code);
        Assert.Equal(3, errors["A1"].Position);
        Assert.Equal(1, errors["A1"].Length);
        Assert.Equal("#DIV0", new WorkbookCalculator(book).Calculate()["B1"].Error?.Code);
    }

    [Fact]
    public void ChangingASourceRecalculatesTheDependencyChain()
    {
        var book = new Workbook();
        book.Set("A1", "10");
        book.Set("B1", "a1*2+D1");
        book.Set("C1", "max(B1,25)");
        var before = new WorkbookCalculator(book).Calculate();
        Assert.Equal(new BigInteger(20), before["B1"].Value);
        Assert.Equal(new BigInteger(25), before["C1"].Value);

        book.Set("A1", "20");
        var after = new WorkbookCalculator(book).Calculate();

        Assert.All(after.Values, result => Assert.Null(result.Error));
        Assert.Equal(new BigInteger(40), after["B1"].Value);
        Assert.Equal(new BigInteger(40), after["C1"].Value);
    }

    [Fact]
    public void IndirectCycleIsReportedAndCalculationRecoversAfterItIsBroken()
    {
        var book = new Workbook();
        book.Set("A1", "B1");
        book.Set("B1", "C1");
        book.Set("C1", "A1");
        book.Set("D1", "B1+1");
        book.Set("E1", "42");
        var cyclic = new WorkbookCalculator(book).Calculate();
        Assert.Equal("#CYCLE", cyclic["A1"].Error?.Code);
        Assert.Equal("#CYCLE", cyclic["D1"].Error?.Code);
        Assert.Equal(new BigInteger(42), cyclic["E1"].Value);

        book.Set("C1", "7");
        var recovered = new WorkbookCalculator(book).Calculate();

        Assert.All(recovered.Values, result => Assert.Null(result.Error));
        Assert.Equal(new BigInteger(7), recovered["A1"].Value);
        Assert.Equal(new BigInteger(8), recovered["D1"].Value);
    }

    [Fact]
    public void JsonRoundTripPreservesTableMetadataFormulasAndReferences()
    {
        var book = new Workbook { Name = "Таблиця їжаків", Rows = 100, Columns = 100 };
        book.Set("A1", "=2^100");
        book.Set("CV100", "max(A1,10)");

        var restored = WorkbookFile.Deserialize(WorkbookFile.Serialize(book));

        Assert.Equal(book.Name, restored.Name);
        Assert.Equal(100, restored.Rows);
        Assert.Equal(100, restored.Columns);
        Assert.Equal(2, restored.Cells.Count);
        Assert.Equal("=2^100", restored.Get("A1"));
        Assert.Equal("max(A1,10)", restored.Get("CV100"));
        var result = new WorkbookCalculator(restored).Calculate()["CV100"];
        Assert.Null(result.Error);
        Assert.Equal(BigInteger.Pow(2, 100), result.Value);
    }
}
