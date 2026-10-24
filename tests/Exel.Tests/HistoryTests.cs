namespace Exel.Tests;
public class HistoryTests
{
    [Fact]public void EmptyHistoryIsClean(){var h=new WorkbookHistory();var b=new Workbook();Assert.False(h.IsDirty);Assert.Null(h.Undo(b,"A1"));Assert.Null(h.Redo(b,"A1"));}
    [Fact]public void UndoRedoRestoreDataAndSelection(){var b=new Workbook();var h=new WorkbookHistory();h.Record(b,"A1");b.Set("A1","42");var state=h.Undo(b,"B2")!;Assert.Empty(state.Book.Cells);Assert.Equal("A1",state.Selection);Assert.False(h.IsDirty);state=h.Redo(state.Book,state.Selection)!;Assert.Equal("42",state.Book.Get("A1"));Assert.Equal("B2",state.Selection);Assert.True(h.IsDirty);}
    [Fact]public void SavedRevisionTracksUndoAndRedo(){var h=new WorkbookHistory();var b=new Workbook();h.Record(b,"A1");b.Set("A1","1");h.MarkSaved();Assert.False(h.IsDirty);var s=h.Undo(b,"A1")!;Assert.True(h.IsDirty);s=h.Redo(s.Book,"A1")!;Assert.False(h.IsDirty);}
    [Fact]public void NewBranchClearsRedoAndRemainsDirty(){var h=new WorkbookHistory();var b=new Workbook();h.Record(b,"A1");b.Set("A1","1");h.MarkSaved();b=h.Undo(b,"A1")!.Book;h.Record(b,"A1");b.Set("A1","2");Assert.False(h.CanRedo);Assert.True(h.IsDirty);}
    [Fact]public void ShrinkUndoRestoresRemovedCells(){var h=new WorkbookHistory();var b=new Workbook{Rows=3,Columns=3,Name="Початок"};b.Set("C3","7");h.Record(b,"C3");b.Rows=2;b.Columns=2;b.Cells.Clear();b.Name="Після";var s=h.Undo(b,"B2")!;Assert.Equal(3,s.Book.Rows);Assert.Equal(3,s.Book.Columns);Assert.Equal("7",s.Book.Get("C3"));Assert.Equal("Початок",s.Book.Name);}
    [Fact]public void HistoryIsBoundedToFortyStates(){var h=new WorkbookHistory();var b=new Workbook();for(int i=1;i<=45;i++){h.Record(b,"A1");b.Set("A1",i.ToString());}int count=0;while(h.CanUndo){b=h.Undo(b,"A1")!.Book;count++;}Assert.Equal(40,count);Assert.Equal("5",b.Get("A1"));}
    [Fact]public void ResetDropsHistoryAndSavedState(){var h=new WorkbookHistory();var b=new Workbook();h.Record(b,"A1");b.Set("A1","1");h.Reset();Assert.False(h.CanUndo);Assert.False(h.CanRedo);Assert.False(h.IsDirty);}
}
