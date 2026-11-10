namespace Exel.Tests;

public class SpreadsheetTests
{
    [Fact]
    public void ArithmeticUsesBigIntegerAndOperatorPrecedence()
    {
        var book = new Workbook();
        book.Set("A1", "2^100+3*4");

        var result = new WorkbookCalculator(book).Calculate()["A1"];

        Assert.Null(result.Error);
        Assert.Equal(BigInteger.Pow(2, 100) + 12, result.Value);
    }

    [Fact]
    public void InvalidFormulaReportsSyntaxErrorAtTheCorrectPosition()
    {
        var error = Assert.Throws<FormulaException>(() => new FormulaParser().Parse("=2+*3"));

        Assert.Equal("#SYNTAX", error.Code);
        Assert.Equal(3, error.Position);
        Assert.Equal(1, error.Length);
    }

    [Fact]
    public void CellReferencesRecalculateAfterTheSourceChanges()
    {
        var book = new Workbook();
        book.Set("A1", "10");
        book.Set("B1", "a1*2");
        var initial = new WorkbookCalculator(book).Calculate()["B1"];
        Assert.Null(initial.Error);
        Assert.Equal(new BigInteger(20), initial.Value);

        book.Set("A1", "15");
        var updated = new WorkbookCalculator(book).Calculate()["B1"];
        Assert.Null(updated.Error);
        Assert.Equal(new BigInteger(30), updated.Value);
    }

    [Fact]
    public void CircularCellReferencesReturnCycleErrors()
    {
        var book = new Workbook();
        book.Set("A1", "B1+1");
        book.Set("B1", "A1+1");

        var results = new WorkbookCalculator(book).Calculate();

        Assert.Equal("#CYCLE", results["A1"].Error?.Code);
        Assert.Equal("#CYCLE", results["B1"].Error?.Code);
    }

    [Fact]
    public void JsonRoundTripPreservesTheTableAndSourceFormulas()
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
    }
}
