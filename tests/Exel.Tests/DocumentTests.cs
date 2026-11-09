using System.Text.Json;
namespace Exel.Tests;

public class DocumentTests
{
    [Theory][InlineData("A1")][InlineData("Z100")][InlineData("AA1")][InlineData("AZ200")]
    public void ValidAddressesFitMaximumSheet(string address)=>Assert.True(CellAddress.InBounds(address,200,52));
    [Theory][InlineData("")][InlineData("A0")][InlineData("A01")][InlineData("AAA1")][InlineData("1A")][InlineData("А1")][InlineData("A2147483648")]
    public void InvalidAddressesAreRejected(string address)=>Assert.False(CellAddress.IsValid(address));
    [Theory][InlineData(0,"A")][InlineData(25,"Z")][InlineData(26,"AA")][InlineData(51,"AZ")]
    public void ColumnNamesCrossAlphabetBoundary(int index,string expected)=>Assert.Equal(expected,CellAddress.Column(index));
    [Fact]
    public void ClearingCellsKeepsSparseStorage()
    {
        var b=new Workbook();b.Set("a1","12");Assert.Equal("12",b.Get("A1"));b.Set("A1","  ");Assert.Empty(b.Cells);
        Assert.Throws<ArgumentException>(()=>b.Set("ZZ1","1"));
    }
    [Fact]
    public void SnapshotIsIndependentAndPreservesMetadata()
    {
        var b=new Workbook{Name="Таблиця їжаків",Rows=5,Columns=4};b.Set("A1","=2^100");var copy=b.Snapshot();copy.Set("A1","7");copy.Name="Інша";
        Assert.Equal("=2^100",b.Get("A1"));Assert.Equal("Таблиця їжаків",b.Name);Assert.Equal(5,copy.Rows);Assert.Equal(4,copy.Columns);
    }
    [Fact]
    public void FileRoundTripPreservesFormulaNotCachedValue()
    {
        var b=new Workbook{Name="Їжак — таблиця",Rows=7,Columns=3};b.Set("a1","=5^8");b.Set("B2","max(A1,10)");
        var json=WorkbookFile.Serialize(b);var restored=WorkbookFile.Deserialize(json);
        Assert.Equal(b.Name,restored.Name);Assert.Equal(7,restored.Rows);Assert.Equal(3,restored.Columns);Assert.Equal("=5^8",restored.Get("a1"));Assert.Equal("max(A1,10)",restored.Get("B2"));
        Assert.DoesNotContain("390625",json);
    }
    [Theory]
    [InlineData("{\"Version\":2}")]
    [InlineData("{\"Rows\":0}")]
    [InlineData("{\"Rows\":201}")]
    [InlineData("{\"Columns\":53}")]
    [InlineData("{\"Name\":\" \"}")]
    [InlineData("{\"Cells\":null}")]
    [InlineData("{\"Cells\":{\"A1\":null}}")]
    [InlineData("{\"Cells\":{\"A01\":\"1\"}}")]
    [InlineData("{\"Cells\":{\"A1\":\"1\",\"a1\":\"2\"}}")]
    public void InvalidDocumentsAreRejected(string json)=>Assert.Throws<InvalidDataException>(()=>WorkbookFile.Deserialize(json));
    [Fact]
    public void InvalidJsonIsRejected()=>Assert.Throws<JsonException>(()=>WorkbookFile.Deserialize("{bad"));
    [Fact]
    public void FileSizeIsBounded()
    {
        Assert.Throws<InvalidDataException>(()=>WorkbookFile.Deserialize(new string(' ',4_000_001)));
        var b=new Workbook{Rows=200,Columns=52};for(int row=1;row<=50;row++)for(int col=0;col<26;col++)b.Cells[CellAddress.Column(col)+row]=new string('1',4096);
        Assert.Throws<InvalidDataException>(()=>WorkbookFile.Serialize(b));
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    [InlineData("\u00A0")]
    public void ImportedBlankCellsBehaveLikeClearedCells(string blank)
    {
        string json = JsonSerializer.Serialize(new { Cells = new Dictionary<string, string> { ["a1"] = blank, ["B1"] = "A1+1" } });
        var imported = WorkbookFile.Deserialize(json);
        Assert.False(imported.Cells.ContainsKey("A1"));
        Assert.Equal("", imported.Get("A1"));
        var result = new WorkbookCalculator(imported).Calculate()["B1"];
        Assert.Null(result.Error);
        Assert.Equal(BigInteger.One, result.Value);
        var reopened = WorkbookFile.Deserialize(WorkbookFile.Serialize(imported));
        Assert.False(reopened.Cells.ContainsKey("A1"));
        Assert.Equal("A1+1", reopened.Get("B1"));
    }
    [Fact]
    public void BlankCellsDoNotBypassAddressValidation()
    {
        Assert.Throws<InvalidDataException>(() => WorkbookFile.Deserialize("""{"Cells":{"CW1":""}}"""));
        Assert.Throws<InvalidDataException>(() => WorkbookFile.Deserialize("""{"Cells":{"A1":"","a1":"2"}}"""));
    }
}
