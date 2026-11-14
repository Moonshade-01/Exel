namespace Exel.Tests;
public class DependencyTests
{
    [Fact] public void BlankReferencesAndLowercaseAddressesWork()
    { var b = new Workbook(); b.Set("A1","10"); b.Set("B1","a1*2+C2"); Assert.Equal(new BigInteger(20),new WorkbookCalculator(b).Calculate()["B1"].Value); }
    [Fact] public void SharedDependenciesAreNotCycles()
    { var b=new Workbook(); b.Set("A1","7"); b.Set("B1","A1*2"); b.Set("C1","A1*3"); b.Set("D1","B1+C1"); var r=new WorkbookCalculator(b).Calculate(); Assert.All(r.Values,x=>Assert.Null(x.Error)); Assert.Equal(new BigInteger(35),r["D1"].Value); }
    [Fact] public void DependencyErrorsPointAtTheCurrentReference()
    { var b=new Workbook(); b.Set("A1","1/0"); b.Set("B1","2+A1"); var e=new WorkbookCalculator(b).Calculate()["B1"].Error!; Assert.Equal("#DIV0",e.Code); Assert.Equal(2,e.Position); Assert.Equal(2,e.Length); }
    [Fact] public void CalculatorUsesAnIndependentSnapshot()
    { var b=new Workbook(); b.Set("A1","1"); var c=new WorkbookCalculator(b); b.Set("A1","99"); Assert.Equal(BigInteger.One,c.Calculate()["A1"].Value); }
    [Fact] public void DeletingTheLastRowInvalidatesItsReferences()
    { var b=new Workbook(); b.Set("A100","7"); b.Set("B1","A100*2"); var before=b.Snapshot(); b.Rows=99; b.Cells.Remove("A100"); Assert.Equal("#REF",new WorkbookCalculator(b).Calculate()["B1"].Error?.Code); Assert.Equal(new BigInteger(14),new WorkbookCalculator(before).Calculate()["B1"].Value); }
    [Fact] public void DeletingTheLastColumnInvalidatesItsReferences()
    { var b=new Workbook(); b.Set("CV100","7"); b.Set("A1","CV100"); b.Columns=99; b.Cells.Remove("CV100"); Assert.Equal("#REF",new WorkbookCalculator(b).Calculate()["A1"].Error?.Code); }
    [Fact] public void FullHundredByHundredSheetCalculatesCorrectly()
    { var b=new Workbook(); for(int r=1;r<=100;r++) for(int c=0;c<100;c++) b.Set(CellAddress.Column(c)+r,r==1?"17div5+2^10":CellAddress.Column(c)+(r-1)+"+1"); var result=new WorkbookCalculator(b).Calculate(); Assert.Equal(10000,result.Count); Assert.All(result.Values,x=>Assert.Null(x.Error)); Assert.Equal(new BigInteger(1126),result["CV100"].Value); }
    [Fact] public void ExcessiveReferenceChainsReturnLimitErrors()
    { var b=new Workbook(); var keys=Enumerable.Range(1,100).Select(r=>"A"+r).Concat(Enumerable.Range(1,40).Select(r=>"B"+r)).ToArray(); for(int i=0;i<keys.Length;i++) b.Set(keys[i],i==keys.Length-1?"1":keys[i+1]); Assert.Equal("#LIMIT",new WorkbookCalculator(b).Calculate()["A1"].Error?.Code); }
    [Fact] public void CancellationStopsParsingAndSyntaxValidation()
    { var b=new Workbook(); b.Set("A1","1"); using var c=new CancellationTokenSource(); c.Cancel(); Assert.Throws<OperationCanceledException>(()=>new WorkbookCalculator(b,c.Token)); Assert.Throws<OperationCanceledException>(()=>SyntaxValidator.Validate(b,c.Token)); }
}
