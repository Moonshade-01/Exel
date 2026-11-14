using System.Text;
namespace Exel.Tests;
public class DocumentTests
{
    [Theory]
    [InlineData("{\"Rows\":0}")][InlineData("{\"Rows\":101}")][InlineData("{\"Columns\":101}")]
    [InlineData("{\"Version\":2}")][InlineData("{\"Name\":\" \"}")][InlineData("{\"Cells\":null}")]
    [InlineData("{\"Cells\":{\"A01\":\"1\"}}")]
    [InlineData("{\"Cells\":{\"A1\":\"1\",\"A1\":\"2\"}}")]
    public void InvalidOrAmbiguousDocumentsAreRejected(string json) => Assert.Throws<InvalidDataException>(()=>WorkbookFile.Deserialize(json));
    [Fact] public void ImportedBlankCellsBehaveAsClearedCells()
    { var b=WorkbookFile.Deserialize("""{"Cells":{"a1":" ","B1":"A1+1"}}"""); Assert.False(b.Cells.ContainsKey("A1")); Assert.Equal(BigInteger.One,new WorkbookCalculator(b).Calculate()["B1"].Value); }
    [Fact] public void BlankCellsCannotBypassAddressAndDuplicateValidation()
    { Assert.Throws<InvalidDataException>(()=>WorkbookFile.Deserialize("""{"Cells":{"CW1":""}}""")); Assert.Throws<InvalidDataException>(()=>WorkbookFile.Deserialize("""{"Cells":{"A1":"","a1":"2"}}""")); }
    [Fact] public void JsonLimitCountsUtf8BytesAtTheBoundary()
    { const int limit=4000000; const string prefix="{\"padding\":\""; const string suffix="\"}"; int available=limit-Encoding.UTF8.GetByteCount(prefix+suffix); string json=prefix+new string('ї',available/2)+new string(' ',available%2)+suffix; Assert.Equal(limit,Encoding.UTF8.GetByteCount(json)); Assert.NotNull(WorkbookFile.Deserialize(json)); Assert.Throws<InvalidDataException>(()=>WorkbookFile.Deserialize(json[..^2]+"ї"+suffix)); }
    [Fact] public void MaximumDimensionsAndReferencesRoundTrip()
    { var b=new Workbook{Name="Їжак"}; b.Set("CV100","2^100"); var c=WorkbookFile.Deserialize(WorkbookFile.Serialize(b)); Assert.Equal("Їжак",c.Name); Assert.Equal("2^100",c.Get("cv100")); Assert.Equal(100,c.Columns); Assert.Equal(100,c.Rows); }
    [Fact] public void ClearingCellsPreservesSparseStorage()
    { var b=new Workbook(); b.Set("a1","1"); b.Set("A1"," "); Assert.Empty(b.Cells); Assert.Throws<ArgumentException>(()=>b.Set("CW1","1")); }
    [Fact] public void SnapshotPreservesMetadataAndDoesNotShareTheDictionary()
    { var b=new Workbook{Version=2,Name="Source",Rows=2,Columns=3}; b.Set("A1","7"); var copy=b.Snapshot(); copy.Set("A1","9"); Assert.Equal(2,copy.Version); Assert.Equal("Source",copy.Name); Assert.Equal("7",b.Get("A1")); }
}
