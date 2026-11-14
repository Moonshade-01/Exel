namespace Exel.Tests;
public class HistoryTests
{
    [Fact] public void EmptyHistoryRefusesUndoAndRedo()
    { var h=new WorkbookHistory(); var b=new Workbook(); Assert.Null(h.Undo(b,"A1")); Assert.Null(h.Redo(b,"A1")); Assert.False(h.IsDirty); }
    [Fact] public void UndoRestoresContentAndSelection()
    { var h=new WorkbookHistory(); var b=new Workbook(); b.Set("A1","1"); h.Record(b,"A1"); b.Set("A1","2"); var state=h.Undo(b,"B1")!; Assert.Equal("1",state.Book.Get("A1")); Assert.Equal("A1",state.Selection); }
    [Fact] public void RedoRestoresTheUndoneState()
    { var h=new WorkbookHistory(); var b=new Workbook(); h.Record(b,"A1"); b.Set("A1","2"); var old=h.Undo(b,"B1")!; var next=h.Redo(old.Book,old.Selection)!; Assert.Equal("2",next.Book.Get("A1")); Assert.Equal("B1",next.Selection); }
    [Fact] public void NewEditAfterUndoClearsRedo()
    { var h=new WorkbookHistory(); var b=new Workbook(); h.Record(b,"A1"); b.Set("A1","2"); b=h.Undo(b,"A1")!.Book; h.Record(b,"A1"); b.Set("A1","3"); Assert.False(h.CanRedo); }
    [Fact] public void ReturningToSavedRevisionClearsDirtyState()
    { var h=new WorkbookHistory(); var b=new Workbook(); h.Record(b,"A1"); b.Set("A1","1"); h.MarkSaved(); h.Record(b,"A1"); b.Set("A1","2"); Assert.True(h.IsDirty); b=h.Undo(b,"A1")!.Book; Assert.False(h.IsDirty); }
    [Fact] public void OnlyFortyStatesAreRetained()
    { var h=new WorkbookHistory(); var b=new Workbook(); for(int i=0;i<45;i++){h.Record(b,"A1");b.Set("A1",i.ToString());} int count=0; while(h.CanUndo){b=h.Undo(b,"A1")!.Book;count++;} Assert.Equal(40,count); }
    [Fact] public void UndoRestoresDeletedDimensionsAndData()
    { var h=new WorkbookHistory(); var b=new Workbook(); b.Set("CV100","9"); h.Record(b,"CV100"); b.Columns=99; b.Cells.Remove("CV100"); var s=h.Undo(b,"A1")!; Assert.Equal(100,s.Book.Columns); Assert.Equal("9",s.Book.Get("CV100")); }
    [Fact] public void ResetClearsHistoryAndSavedRevision()
    { var h=new WorkbookHistory(); var b=new Workbook(); h.Record(b,"A1"); b.Set("A1","2"); h.Reset(); Assert.False(h.CanUndo); Assert.False(h.CanRedo); Assert.False(h.IsDirty); }
}
