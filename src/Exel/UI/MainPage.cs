using Exel.Core;
using Exel.Services;
using Path = System.IO.Path;
namespace Exel;

public sealed partial class MainPage : ContentPage
{
    private Workbook workbook = new();
    private readonly WorkbookHistory history = new();
    private readonly GoogleDriveService drive = new(openBrowser: async uri => { await Browser.Default.OpenAsync(uri, BrowserLaunchMode.External); });
    private readonly LocalDocuments documents = new();
    private Dictionary<string, CellResult> results = new(StringComparer.OrdinalIgnoreCase);
    private string selected = "A1";
    private string? localPath, driveId;
    private bool busy, initialized, calculationDirty = true, valuesMode = true, referencePicking, referenceFromCell;
    private CancellationTokenSource? calculation;
    private string syntaxState = "Синтаксис";
    private bool DraftChanged => (formula.Text ?? "") != workbook.Get(selected);
    private bool Dirty => history.IsDirty || DraftChanged;

    public MainPage()
    {
        Title = ""; BackgroundColor = Palette.Background;
        BuildLayout(); BuildMenus();
        formula.Focused += (_, _) => sheet.ReleaseKeyboard();
        addressBox.Focused += (_, _) => sheet.ReleaseKeyboard();
        formula.TextChanged += (_, e) => { sheet.SyncDraft(e.NewTextValue ?? ""); UpdateCommands(); };
        formula.Completed += (_, _) => Run(async () => { await ApplyAsync(); await MoveAsync(1, 0); });
        addressBox.Completed += (_, _) => Run(GoToAsync);
        sheet.CellSelected += key => Run(() => referencePicking ? InsertReferenceAsync(key) : SelectAsync(key));
        sheet.CellEditRequested += key => Run(async () => { await SelectAsync(key); sheet.BeginEdit(formula.Text ?? ""); });
        sheet.DraftChanged += text => { if (formula.Text != text) formula.Text = text; };
        sheet.EditCommitted += across => Run(async () => { await ApplyAsync(); await MoveAsync(across ? 0 : 1, across ? 1 : 0); });
        sheet.EditCancelled += () => { if (busy) return; EndReferencePick(); sheet.EndEdit(); formula.Text = workbook.Get(selected); Refresh(); };
        formula.Text = workbook.Get(selected); Refresh(reset: true);
        Loaded += (_, _) =>
        {
            if (initialized) return; initialized = true;
            Run(async () => { await RefreshLibraryAsync(); await RecalculateAsync(); });
        };
    }
    private async void Run(Func<Task> action)
    {
        if (busy) { status.Text = "Дочекайтеся завершення операції або скасуйте обчислення."; return; }
        busy = true; activity.IsRunning = true; UpdateCommands();
        try { await action(); }
        catch (OperationCanceledException) { calculationDirty = true; results.Clear(); Refresh(); status.Text = "Операцію скасовано. Зміни збережено в документі; його можна обчислити повторно."; }
        catch (Exception ex) { await DisplayAlertAsync("Не вдалося виконати дію", ex.Message, "Зрозуміло"); }
        finally { busy = false; activity.IsRunning = false; UpdateCommands(); }
    }
    public bool SelectAllCellText(bool requireCellFocus = true)
    {
        if (busy || requireCellFocus && !sheet.HasCellFocus) return false;
        sheet.BeginEdit(formula.Text ?? ""); return true;
    }
    public bool Shortcut(string command)
    {
        if (command is "undo" or "redo" && (sheet.IsEditing || !sheet.HasCellFocus && (formula.IsFocused || addressBox.IsFocused))) return false;
        Func<Task>? action = command switch
        {
            "save" => SaveAsync, "open" => OpenAsync, "new" => NewAsync,
            "undo" => UndoAsync, "redo" => RedoAsync,
            _ => null
        };
        if (action is null) return false;
        Run(action); return true;
    }
    public bool WorksheetKey(global::Windows.System.VirtualKey key, bool shift, bool control)
    {
        if (!sheet.HasCellFocus || sheet.IsEditing) return false;
        Func<Task>? action = key switch
        {
            global::Windows.System.VirtualKey.Left => () => MoveAsync(0, -1),
            global::Windows.System.VirtualKey.Right => () => MoveAsync(0, 1),
            global::Windows.System.VirtualKey.Up => () => MoveAsync(-1, 0),
            global::Windows.System.VirtualKey.Down => () => MoveAsync(1, 0),
            global::Windows.System.VirtualKey.Enter => () => MoveAsync(shift ? -1 : 1, 0),
            global::Windows.System.VirtualKey.Tab => () => MoveAsync(0, shift ? -1 : 1),
            global::Windows.System.VirtualKey.PageDown => () => MoveAsync(12, 0),
            global::Windows.System.VirtualKey.PageUp => () => MoveAsync(-12, 0),
            global::Windows.System.VirtualKey.Home => () => SelectAsync(control ? "A1" : "A" + Coordinates(selected).Row),
            global::Windows.System.VirtualKey.Delete => ClearAsync,
            _ => null
        };
        if (action is null) return false;
        Run(action); return true;
    }
    private static (int Row, int Column) Coordinates(string key)
    {
        int column = 0, at = 0;
        while (at < key.Length && char.IsAsciiLetter(key[at])) column = column * 26 + key[at++] - 'A' + 1;
        return (int.Parse(key[at..]), column);
    }
    private void InvalidateCalculation()
    {
        calculationDirty = true; syntaxState = "Синтаксис"; results.Clear();
    }
    private bool CommitDraft()
    {
        string text = formula.Text ?? "";
        EndReferencePick(); sheet.EndEdit();
        if (text == workbook.Get(selected)) return false;
        if (text.Length > FormulaParser.MaxFormulaLength) throw new ArgumentException("Формула перевищує 4096 символів.");
        history.Record(workbook, selected); workbook.Set(selected, text); InvalidateCalculation(); return true;
    }
    private void Refresh(bool reset = false)
    {
        sheet.IsReferencePicking = referencePicking;
        sheet.Present(workbook, results, selected, valuesMode, reset);
        addressBox.Text = selected;
        cellCaption.Text = selected + (valuesMode ? "  /  ЗНАЧЕННЯ" : "  /  ФОРМУЛА");
        var current = results.GetValueOrDefault(selected);
        detail.TextColor = current?.Error is null ? Palette.Muted : Palette.Error;
        if (current?.Error is { } error)
            detail.Text = $"{error.Code} · символ {error.Position + 1}. {error.Message}";
        else if (string.IsNullOrWhiteSpace(workbook.Get(selected))) detail.Text = "Порожня клітинка. Введіть число або формулу. F2 — редагувати, Enter — застосувати.";
        else if (calculationDirty) detail.Text = "Формулу змінено. Натисніть «Обчислити», щоб отримати значення.";
        else
        {
            string text = current?.Display ?? "";
            detail.Text = text.Length > 240 ? text[..240] + "…  Відкрити повністю: меню «Вигляд»." : text;
        }
        sizeCaption.Text = $"{workbook.Rows} × {workbook.Columns}   ·   Заповнено {workbook.Cells.Count}";
        modeCaption.Text = valuesMode ? "Значення" : "Формули";
        int errors = results.Count(p => p.Value.Error is not null);
        problemButton.Text = errors == 0 ? "Без помилок" : $"Помилки · {errors}";
        problemButton.TextColor = errors == 0 ? Palette.Success : Palette.Error;
        UpdateCommands();
    }
    private void UpdateCommands()
    {
        documentTitle.Text = workbook.Name;
        savedCaption.Text = Dirty ? "Є незбережені зміни" : "Усі зміни збережено";
        savedCaption.TextColor = Dirty ? Palette.Accent : Palette.Muted;
        sourceCaption.Text = driveId is not null ? "Google Drive" : localPath is not null ? Path.GetFileName(localPath) : "Новий документ";
        modeButton.Text = valuesMode ? "ƒx Формули" : "123 Значення";
        undoButton.IsEnabled = history.CanUndo && !busy; redoButton.IsEnabled = history.CanRedo && !busy;
        syntaxButton.Text = syntaxState;
        referenceButton.Text = referencePicking ? "Обрати клітинку…" : "↗ Посилання";
        referenceButton.BackgroundColor = referencePicking ? Palette.Selected : Palette.Header;
        referenceButton.TextColor = referencePicking ? Palette.Accent : Palette.Muted;
        if (Window is not null) WindowCaption.Set(Window, $"Exel — {workbook.Name}{(Dirty ? " *" : "")}");
    }
    private async Task RecalculateAsync()
    {
        status.Text = "Перевірка синтаксису й обчислення…";
        var snapshot = workbook.Snapshot();
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5)); calculation = cancel;
        try
        {
            results = await Task.Run(() => new WorkbookCalculator(snapshot, cancel.Token).Calculate(), cancel.Token);
            calculationDirty = false;
            syntaxState = results.Values.Any(r => r.Error?.Code == "#SYNTAX") ? "Синтаксис !" : "Синтаксис ✓";
        }
        finally { calculation = null; }
        Refresh();
        status.Text = results.Values.Any(r => r.Error is not null) ? "Обчислення завершено. Пояснення помилок доступні внизу та у списку проблем." : "Готово";
    }
    private async Task ApplyAsync() { CommitDraft(); if (calculationDirty) await RecalculateAsync(); else Refresh(); }
    private async Task ForceCalculateAsync() { CommitDraft(); await RecalculateAsync(); }
    private async Task SyntaxAsync()
    {
        CommitDraft(); var snapshot = workbook.Snapshot();
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5)); calculation = cancel;
        Dictionary<string, FormulaException> errors;
        try { errors = await Task.Run(() => SyntaxValidator.Validate(snapshot, cancel.Token), cancel.Token); }
        finally { calculation = null; }
        if (calculationDirty) results = errors.ToDictionary(p => p.Key, p => new CellResult(null, p.Value), StringComparer.OrdinalIgnoreCase);
        syntaxState = errors.Count == 0 ? "Синтаксис ✓" : $"Синтаксис · {errors.Count}";
        Refresh(); status.Text = errors.Count == 0 ? $"Синтаксис {workbook.Cells.Count} формул коректний. Арифметичні дії не виконувалися." : $"Синтаксичних помилок: {errors.Count}. Натисніть «Помилки», щоб переглянути.";
    }
    private async Task SelectAsync(string key)
    {
        bool changed = CommitDraft(); selected = key; formula.Text = workbook.Get(key);
        if (changed) await RecalculateAsync(); else Refresh();
        sheet.EnsureVisible(selected); sheet.FocusGrid(); sheet.PulseSelection();
    }
    private async Task MoveAsync(int rows, int columns)
    {
        var at = Coordinates(selected);
        string next = CellAddress.Column(Math.Clamp(at.Column + columns, 1, workbook.Columns) - 1) + Math.Clamp(at.Row + rows, 1, workbook.Rows);
        await SelectAsync(next);
    }
    private async Task GoToAsync()
    {
        string key = (addressBox.Text ?? "").Trim().ToUpperInvariant();
        if (!CellAddress.InBounds(key, workbook.Rows, workbook.Columns)) throw new ArgumentException("Адреса поза таблицею. Приклад: A1 або Z100.");
        await SelectAsync(key);
    }
    private async Task ToggleAsync() { CommitDraft(); valuesMode = !valuesMode; if (calculationDirty) await RecalculateAsync(); else Refresh(); }
    private async Task ClearAsync() { formula.Text = ""; await ApplyAsync(); }
    private Task ReferenceAsync()
    {
        referenceFromCell = sheet.IsEditing; referencePicking = !referencePicking;
        sheet.IsReferencePicking = referencePicking; UpdateCommands();
        status.Text = referencePicking ? $"Оберіть клітинку для вставлення її адреси у формулу {selected}." : "Вставлення посилання скасовано.";
        return Task.CompletedTask;
    }
    private void EndReferencePick() { referencePicking = false; sheet.IsReferencePicking = false; }
    private Task InsertReferenceAsync(string key)
    {
        string text = formula.Text ?? "";
        var caret = referenceFromCell && sheet.IsEditing ? sheet.Caret : (formula.CursorPosition, formula.SelectionLength);
        int start = Math.Clamp(caret.Item1, 0, text.Length), length = Math.Clamp(caret.Item2, 0, text.Length - start);
        string next = text[..start] + key + text[(start + length)..];
        if (next.Length > FormulaParser.MaxFormulaLength) throw new ArgumentException("Формула перевищує 4096 символів.");
        EndReferencePick(); formula.Text = next;
        if (referenceFromCell && sheet.IsEditing) sheet.SetCaret(start + key.Length);
        else { formula.Focus(); formula.CursorPosition = start + key.Length; formula.SelectionLength = 0; }
        sheet.ShowReference(key); UpdateCommands(); status.Text = $"Посилання {key} вставлено у формулу {selected}. Enter — застосувати.";
        return Task.CompletedTask;
    }
    private async Task UndoAsync()
    {
        CommitDraft(); var state = history.Undo(workbook, selected); if (state is null) return;
        await RestoreAsync(state); status.Text = "Попередню дію скасовано.";
    }
    private async Task RedoAsync()
    {
        if (DraftChanged) CommitDraft();
        var state = history.Redo(workbook, selected); if (state is null) return;
        await RestoreAsync(state); status.Text = "Дію повторено.";
    }
    private async Task RestoreAsync(DocumentState state)
    {
        EndReferencePick(); sheet.EndEdit(); workbook = state.Book.Snapshot(); selected = state.Selection;
        formula.Text = workbook.Get(selected); InvalidateCalculation(); await RecalculateAsync(); sheet.EnsureVisible(selected);
    }
    private async Task ResizeAsync(bool row, bool add)
    {
        CommitDraft();
        int current = row ? workbook.Rows : workbook.Columns;
        if (add && current >= (row ? 200 : 52) || !add && current <= 1)
        { status.Text = "Межі таблиці: від 1 × 1 до 200 × 52."; return; }
        history.Record(workbook, selected);
        if (row) workbook.Rows += add ? 1 : -1; else workbook.Columns += add ? 1 : -1;
        foreach (string key in workbook.Cells.Keys.Where(k => !CellAddress.InBounds(k, workbook.Rows, workbook.Columns)).ToArray()) workbook.Cells.Remove(key);
        var at = Coordinates(selected); selected = CellAddress.Column(Math.Min(at.Column, workbook.Columns) - 1) + Math.Min(at.Row, workbook.Rows);
        formula.Text = workbook.Get(selected); InvalidateCalculation(); await RecalculateAsync(); sheet.EnsureVisible(selected);
        status.Text = add ? "Таблицю розширено." : "Останній рядок або стовпець видалено. Ctrl+Z — повернути.";
    }
    private async Task RenameAsync()
    {
        string? name = await DisplayPromptAsync("Назва документа", "Введіть назву:", "Застосувати", "Скасувати", maxLength: 120, initialValue: workbook.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == workbook.Name) return;
        CommitDraft(); history.Record(workbook, selected); workbook.Name = name.Trim(); Refresh();
    }
    private async Task<bool> CanReplaceAsync()
    {
        if (!Dirty) return true;
        string? choice = await DisplayActionSheetAsync("Зберегти зміни документа перед продовженням?", "Скасувати", null, "Зберегти", "Не зберігати");
        if (choice == "Не зберігати") return true;
        if (choice != "Зберегти") return false;
        try { await SaveAsync(); return !Dirty; }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Не вдалося зберегти документ", ex.Message, "Зрозуміло");
            return false;
        }
    }
    public async Task<bool> CanCloseAsync() { if (busy) { calculation?.Cancel(); return false; } return await CanReplaceAsync(); }
    private async Task NewAsync() { if (await CanReplaceAsync()) await SetDocumentAsync(new(), null, null); }
    private async Task SetDocumentAsync(Workbook book, string? path, string? cloudId)
    {
        EndReferencePick(); sheet.EndEdit(); workbook = book; localPath = path; driveId = cloudId; selected = "A1";
        history.Reset(); formula.Text = workbook.Get(selected); InvalidateCalculation(); Refresh(reset: true); await RecalculateAsync();
    }
    private async Task OpenAsync()
    {
        if (!await CanReplaceAsync()) return;
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Відкрити таблицю Exel (.json)", FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { [DevicePlatform.WinUI] = new string[] { ".json" } }) });
        if (file is not null) await LoadLocalAsync(file.FullPath, false);
    }
    private async Task LoadLocalAsync(string path, bool confirm = true)
    {
        if (confirm && !await CanReplaceAsync()) return;
        if (new FileInfo(path).Length > 4_000_000) throw new InvalidDataException("Файл перевищує 4 МБ.");
        string json = await File.ReadAllTextAsync(path);
        var book = await Task.Run(() => WorkbookFile.Deserialize(json));
        await SetDocumentAsync(book, path, null); documents.Remember(path); await RefreshLibraryAsync();
    }
    private async Task OpenSampleAsync(string fileName)
    {
        if (!await CanReplaceAsync()) return;
        await using var stream = await FileSystem.Current.OpenAppPackageFileAsync("Samples/" + fileName);
        using var reader = new StreamReader(stream); string json = await reader.ReadToEndAsync();
        var book = await Task.Run(() => WorkbookFile.Deserialize(json)); await SetDocumentAsync(book, null, null);
    }
    private Task SaveAsync() => SaveLocalAsync(false);
    private Task SaveAsAsync() => SaveLocalAsync(true);
    private async Task SaveLocalAsync(bool choose)
    {
        CommitDraft();
        string? destination = !choose ? localPath : null;
        destination ??= await NativeFileDialogs.SaveAsync(this, workbook.Name, documents.Folder);
        if (destination is null) return;
        var snapshot = workbook.Snapshot(); string json = await Task.Run(() => WorkbookFile.Serialize(snapshot));
        string temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temp, json); File.Move(temp, destination, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        localPath = destination; history.MarkSaved(); documents.Remember(destination); Refresh(); await RefreshLibraryAsync(); status.Text = "Документ збережено.";
    }
    private async Task PickFolderAsync()
    {
        string? folder = await NativeFileDialogs.FolderAsync(this);
        if (folder is null) return;
        documents.SetFolder(folder); await RefreshLibraryAsync();
    }
    private async Task DriveAsync()
    {
        string? choice = await DisplayActionSheetAsync("Google Drive", "Скасувати", null,
            drive.Connected ? "Змінити обліковий запис" : "Підключити Google Drive", "Відкрити з Drive", "Зберегти в Drive", "Вийти");
        if (choice is "Підключити Google Drive" or "Змінити обліковий запис")
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "OAuth Desktop client JSON" });
            if (file is null) return;
            await using var stream = await file.OpenReadAsync(); using var reader = new StreamReader(stream);
            drive.Configure(await reader.ReadToEndAsync()); driveId = null; await drive.SignInAsync();
            driveCaption.Text = "Google Drive підключено"; status.Text = "Google Drive підключено.";
        }
        else if (choice == "Зберегти в Drive")
        {
            CommitDraft(); driveId = await drive.SaveAsync(workbook.Snapshot(), driveId); history.MarkSaved(); Refresh(); status.Text = "Збережено в Google Drive.";
        }
        else if (choice == "Відкрити з Drive")
        {
            if (!await CanReplaceAsync()) return;
            var files = await drive.ListAsync();
            if (files.Count == 0) { await DisplayAlertAsync("Google Drive", "Немає таблиць, створених цим застосунком.", "Зрозуміло"); return; }
            string[] names = files.Select((f, i) => $"{i + 1}. {f.Name}").ToArray();
            string? name = await DisplayActionSheetAsync("Оберіть таблицю", "Скасувати", null, names);
            int at = Array.IndexOf(names, name); if (at < 0) return;
            await SetDocumentAsync(await drive.DownloadAsync(files[at].Id), null, files[at].Id);
        }
        else if (choice == "Вийти") { drive.Disconnect(); driveId = null; driveCaption.Text = "Google Drive не підключено"; UpdateCommands(); }
    }
    private async Task FullValueAsync()
    {
        await ApplyAsync(); var result = results.GetValueOrDefault(selected);
        await ShowTextAsync(selected + " · Повне значення", result?.Error?.Message ?? result?.Display ?? "Порожня клітинка");
    }
    private Task ProblemsAsync()
    {
        var errors = results.Where(p => p.Value.Error is not null).ToArray();
        return ShowTextAsync("Діагностика документа", errors.Length == 0
            ? calculationDirty ? "Синтаксичних помилок не знайдено. Для перевірки значень і залежностей виконайте обчислення." : "Помилок немає."
            : string.Join("\n\n", errors.Select(p => $"{p.Key} · {p.Value.Error!.Code} · символ {p.Value.Error.Position + 1}\n{p.Value.Error.Message}")));
    }
}
