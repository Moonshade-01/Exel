using Exel.Core;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
namespace Exel;

public sealed class SheetView : ContentView, IDrawable
{
    private const float RowHeight = 34, ColumnWidth = 144, RowHeader = 46, ScrollSize = 12;
    private readonly GraphicsView canvas;
    private readonly Entry cellEditor = new() { IsVisible = false, FontFamily = "Consolas", FontSize = 13, TextColor = Palette.Text, BackgroundColor = Palette.Selected, MaxLength = FormulaParser.MaxFormulaLength };
    private readonly AbsoluteLayout viewport = new() { IsClippedToBounds = true };
    private Workbook? workbook;
    private IReadOnlyDictionary<string, CellResult> results = new Dictionary<string, CellResult>();
    private string selected = "A1", lastClicked = "", reference = "";
    private long lastClickTime;
    private bool editing, selectionPending, valuesMode;
    private int topRow, firstColumn, dragAxis;
    private float selectionPulse;
    public bool HasCellFocus { get; private set; }
    public bool IsEditing => editing;
    public bool IsReferencePicking { get; set; }
    public event Action<string>? CellSelected;
    public event Action<string>? CellEditRequested;
    public event Action<string>? DraftChanged;
    public event Action<bool>? EditCommitted;
    public event Action? EditCancelled;
    public (int, int) Caret => cellEditor.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox native
        ? (native.SelectionStart, native.SelectionLength) : (cellEditor.CursorPosition, cellEditor.SelectionLength);
    private float GridWidth => Math.Max(RowHeader, (float)canvas.Width - ScrollSize);
    private float GridHeight => Math.Max(RowHeight, (float)canvas.Height - ScrollSize);
    private int VisibleRows => Math.Max(1, (int)((GridHeight - RowHeight) / RowHeight));
    private int VisibleColumns => Math.Max(1, (int)((GridWidth - RowHeader) / ColumnWidth));
    public SheetView()
    {
        canvas = new GraphicsView { Drawable = this, BackgroundColor = Palette.Surface, MinimumHeightRequest = 100 };
        AbsoluteLayout.SetLayoutFlags(canvas, AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(canvas, new Rect(0, 0, 1, 1));
        viewport.Add(canvas); viewport.Add(cellEditor); Content = viewport;
        canvas.SizeChanged += (_, _) => UpdateViewport();
        viewport.SizeChanged += (_, _) => UpdateViewport();
        canvas.StartInteraction += (_, e) =>
        {
            if (e.Touches.Length == 0 || workbook is null) return;
            var p = e.Touches[0];
            if (p.X >= GridWidth && p.Y >= RowHeight) { dragAxis = 1; DragScroll(p); return; }
            if (p.Y >= GridHeight && p.X >= RowHeader) { dragAxis = 2; DragScroll(p); return; }
            var active = Split(selected);
            int column = p.X < RowHeader ? active.Column : firstColumn + (int)((p.X - RowHeader) / ColumnWidth);
            int row = p.Y < RowHeight ? active.Row : topRow + (int)((p.Y - RowHeight) / RowHeight);
            if (column < 0 || row < 0 || column >= workbook.Columns || row >= workbook.Rows) return;
            string key = CellAddress.Column(column) + (row + 1);
            HasCellFocus = true;
            if (IsReferencePicking) { CellSelected?.Invoke(key); return; }
            long now = Environment.TickCount64;
            bool doubleClick = key == lastClicked && now - lastClickTime < 500 && p.X >= RowHeader && p.Y >= RowHeight;
            lastClicked = key; lastClickTime = doubleClick ? 0 : now;
            if (doubleClick) CellEditRequested?.Invoke(key);
            else { FocusGrid(); CellSelected?.Invoke(key); }
        };
        canvas.DragInteraction += (_, e) => { if (dragAxis != 0 && e.Touches.Length > 0) DragScroll(e.Touches[0]); };
        canvas.EndInteraction += (_, _) => dragAxis = 0;
        cellEditor.TextChanged += (_, e) => { if (editing) DraftChanged?.Invoke(e.NewTextValue ?? ""); };
        cellEditor.Completed += (_, _) => EditCommitted?.Invoke(false);
        cellEditor.HandlerChanged += (_, _) =>
        {
            if (cellEditor.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox native)
                native.PreviewKeyDown += (_, e) =>
                {
                    bool control = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
                    if (control && e.Key == Windows.System.VirtualKey.A) { native.SelectAll(); e.Handled = true; }
                    else if (e.Key == Windows.System.VirtualKey.Escape) { e.Handled = true; EditCancelled?.Invoke(); }
                    else if (e.Key == Windows.System.VirtualKey.Tab) { e.Handled = true; EditCommitted?.Invoke(true); }
                };
        };
        canvas.HandlerChanged += (_, _) =>
        {
            if (canvas.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement native)
            {
                if (native is Microsoft.UI.Xaml.Controls.Control control) control.IsTabStop = true;
                native.PointerWheelChanged += (_, e) =>
                {
                    if (workbook is null) return;
                    var point = e.GetCurrentPoint(native);
                    if (editing && !IsReferencePicking) EditCommitted?.Invoke(false);
                    bool horizontal = point.Properties.IsHorizontalMouseWheel ||
                        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
                    if (horizontal) firstColumn += point.Properties.MouseWheelDelta > 0 ? -1 : 1;
                    else topRow += point.Properties.MouseWheelDelta > 0 ? -3 : 3;
                    UpdateViewport(); e.Handled = true;
                };
            }
        };
        SemanticProperties.SetDescription(canvas, "Електронна таблиця. Стрілки — навігація, F2 — редагування, Ctrl+A — текст клітинки. Колесо — рядки, Shift+колесо — стовпці.");
    }
    private static (int Row, int Column) Split(string key)
    {
        int column = 0, at = 0;
        while (at < key.Length && char.IsAsciiLetter(key[at])) column = column * 26 + key[at++] - 'A' + 1;
        return (int.Parse(key[at..]) - 1, column - 1);
    }
    public void Present(Workbook book, IReadOnlyDictionary<string, CellResult> calculated, string address, bool showValues, bool reset = false)
    {
        if (selected != address || reset) EndEdit();
        workbook = book; results = calculated; selected = address; valuesMode = showValues;
        if (reset) { topRow = firstColumn = 0; }
        UpdateViewport();
    }
    public void FocusGrid()
    {
        HasCellFocus = true;
        if (canvas.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control native) native.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }
    public void ReleaseKeyboard() { HasCellFocus = false; selectionPending = false; }
    public void PulseSelection() => new Animation(v => { selectionPulse = (float)v; canvas.Invalidate(); }, 1, 0).Commit(canvas, "SelectionPulse", 16, 180, Easing.CubicOut);
    public void EnsureVisible(string address)
    {
        if (workbook is null) return;
        var at = Split(address);
        if (at.Row < topRow) topRow = at.Row; else if (at.Row >= topRow + VisibleRows) topRow = at.Row - VisibleRows + 1;
        if (at.Column < firstColumn) firstColumn = at.Column; else if (at.Column >= firstColumn + VisibleColumns) firstColumn = at.Column - VisibleColumns + 1;
        UpdateViewport();
    }
    private void UpdateViewport()
    {
        if (workbook is null) return;
        topRow = Math.Clamp(topRow, 0, Math.Max(0, workbook.Rows - VisibleRows));
        firstColumn = Math.Clamp(firstColumn, 0, Math.Max(0, workbook.Columns - VisibleColumns));
        UpdateEditorBounds(); canvas.Invalidate();
    }
    public void BeginEdit(string text)
    {
        EnsureVisible(selected); editing = true; selectionPending = true; HasCellFocus = true;
        cellEditor.Text = text; cellEditor.IsVisible = true; UpdateEditorBounds();
        Dispatcher.Dispatch(FocusEditor); Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(60), FocusEditor);
    }
    private void FocusEditor()
    {
        if (!editing || !selectionPending || !cellEditor.IsVisible) return;
        if (cellEditor.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox native)
        {
            if (!native.Focus(Microsoft.UI.Xaml.FocusState.Programmatic)) return;
            native.SelectAll(); selectionPending = false;
        }
    }
    public void SetCaret(int position)
    {
        EnsureVisible(selected);
        selectionPending = false;
        if (cellEditor.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox native)
        { native.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); native.Select(Math.Clamp(position, 0, native.Text.Length), 0); }
        else { cellEditor.Focus(); cellEditor.CursorPosition = position; cellEditor.SelectionLength = 0; }
    }
    public void SyncDraft(string text) { if (editing && cellEditor.Text != text) cellEditor.Text = text; }
    public void EndEdit() { editing = false; selectionPending = false; reference = ""; cellEditor.IsVisible = false; cellEditor.Unfocus(); }
    public void ShowReference(string key) { reference = key; canvas.Invalidate(); }
    private void UpdateEditorBounds()
    {
        if (!editing) return;
        var at = Split(selected);
        float x = RowHeader + (at.Column - firstColumn) * ColumnWidth, y = RowHeight * (at.Row - topRow + 1);
        cellEditor.IsVisible = x >= RowHeader && y >= RowHeight && x < GridWidth && y < GridHeight;
        AbsoluteLayout.SetLayoutBounds(cellEditor, new Rect(x + 1, y + 1, ColumnWidth - 2, RowHeight - 2));
        SemanticProperties.SetDescription(cellEditor, $"Клітинка {selected}. Ctrl+A — весь текст; Enter — застосувати; Escape — скасувати.");
    }
    private void DragScroll(PointF p)
    {
        if (workbook is null) return;
        if (dragAxis == 1)
        {
            float track = Math.Max(1, GridHeight - RowHeight), thumb = Math.Clamp(track * VisibleRows / workbook.Rows, 24, track);
            topRow = (int)Math.Round(Math.Clamp((p.Y - RowHeight - thumb / 2) / Math.Max(1, track - thumb), 0, 1) * Math.Max(0, workbook.Rows - VisibleRows));
        }
        else
        {
            float track = Math.Max(1, GridWidth - RowHeader), thumb = Math.Clamp(track * VisibleColumns / workbook.Columns, 24, track);
            firstColumn = (int)Math.Round(Math.Clamp((p.X - RowHeader - thumb / 2) / Math.Max(1, track - thumb), 0, 1) * Math.Max(0, workbook.Columns - VisibleColumns));
        }
        UpdateViewport();
    }
    public void Draw(ICanvas painter, RectF dirtyRect)
    {
        float width = Math.Max(0, (float)canvas.Width), height = Math.Max(0, (float)canvas.Height);
        painter.FillColor = Palette.Surface; painter.FillRectangle(0, 0, width, height);
        if (workbook is null || width < RowHeader || height < RowHeight) return;
        painter.Font = new Microsoft.Maui.Graphics.Font("Consolas"); painter.FontSize = 12;
        var active = Split(selected);
        int rows = Math.Min(workbook.Rows - topRow, (int)Math.Ceiling((GridHeight - RowHeight) / RowHeight));
        int columns = Math.Min(workbook.Columns - firstColumn, (int)Math.Ceiling((GridWidth - RowHeader) / ColumnWidth));
        painter.SaveState(); painter.ClipRectangle(0, 0, GridWidth, GridHeight);
        for (int r = 0; r < rows; r++)
        {
            float y = RowHeight * (r + 1);
            for (int c = 0; c < columns; c++)
            {
                float x = RowHeader + c * ColumnWidth;
                string key = CellAddress.Column(c + firstColumn) + (r + topRow + 1);
                results.TryGetValue(key, out var result);
                string text = valuesMode ? result?.Display ?? "" : workbook.Get(key);
                if (text.Length > 18) text = text[..17] + "…";
                painter.FillColor = key == selected ? Palette.Selected : result?.Error is not null ? Palette.ErrorBackground : Palette.Surface;
                painter.FillRectangle(x, y, ColumnWidth, RowHeight);
                painter.StrokeColor = Palette.Line; painter.StrokeSize = 0.7f; painter.DrawRectangle(x, y, ColumnWidth, RowHeight);
                painter.FontColor = result?.Error is not null ? Palette.Error : Palette.Text;
                painter.SaveState(); painter.ClipRectangle(x + 7, y, ColumnWidth - 14, RowHeight);
                painter.DrawString(text, x + 8, y, 1200, RowHeight, HorizontalAlignment.Left, VerticalAlignment.Center);
                painter.RestoreState();
                if (key == selected || key == reference)
                {
                    painter.StrokeColor = key == selected ? Palette.Accent : Color.FromArgb("8CAEFF");
                    painter.StrokeSize = key == selected ? 1.7f + selectionPulse : 2;
                    painter.DrawRectangle(x + 1, y + 1, ColumnWidth - 2, RowHeight - 2);
                }
            }
            Header(painter, (r + topRow + 1).ToString(), 0, y, RowHeader, r + topRow == active.Row);
        }
        for (int c = 0; c < columns; c++) Header(painter, CellAddress.Column(c + firstColumn), RowHeader + c * ColumnWidth, 0, ColumnWidth, c + firstColumn == active.Column);
        Header(painter, "", 0, 0, RowHeader, false);
        painter.RestoreState();
        DrawScrollbars(painter);
    }
    private static void Header(ICanvas painter, string text, float x, float y, float width, bool active)
    {
        painter.FillColor = active ? Palette.Selected : Palette.Header; painter.FillRectangle(x, y, width, RowHeight);
        painter.StrokeColor = Palette.Line; painter.StrokeSize = 0.7f; painter.DrawRectangle(x, y, width, RowHeight);
        painter.FontColor = active ? Palette.Accent : Palette.Muted;
        painter.DrawString(text, x, y, width, RowHeight, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
    private void DrawScrollbars(ICanvas painter)
    {
        if (workbook is null) return;
        painter.FillColor = Palette.Background;
        painter.FillRectangle(GridWidth, 0, ScrollSize, GridHeight + ScrollSize);
        painter.FillRectangle(0, GridHeight, GridWidth, ScrollSize);
        painter.FillColor = Palette.Muted;
        if (workbook.Rows > VisibleRows)
        {
            float track = GridHeight - RowHeight, thumb = Math.Clamp(track * VisibleRows / workbook.Rows, 24, track);
            float y = RowHeight + (track - thumb) * topRow / Math.Max(1, workbook.Rows - VisibleRows);
            painter.FillRoundedRectangle(GridWidth + 3, y, 6, thumb, 3);
        }
        if (workbook.Columns > VisibleColumns)
        {
            float track = GridWidth - RowHeader, thumb = Math.Clamp(track * VisibleColumns / workbook.Columns, 24, track);
            float x = RowHeader + (track - thumb) * firstColumn / Math.Max(1, workbook.Columns - VisibleColumns);
            painter.FillRoundedRectangle(x, GridHeight + 3, thumb, 6, 3);
        }
    }
}
