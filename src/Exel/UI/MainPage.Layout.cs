using Microsoft.Maui.Controls.Shapes;
using Exel.Core;
using Path = System.IO.Path;
namespace Exel;

public sealed partial class MainPage
{
    private readonly SheetView sheet = new();
    private readonly Entry formula = new() { Placeholder = "Число або формула: =max(A1,B2)+5^8", FontFamily = "Consolas", FontSize = 14, TextColor = Palette.Text, BackgroundColor = Palette.Surface, MaxLength = FormulaParser.MaxFormulaLength };
    private readonly Entry addressBox = new() { Text = "A1", WidthRequest = 76, FontSize = 13, HorizontalTextAlignment = TextAlignment.Center, FontAttributes = FontAttributes.Bold, TextColor = Palette.Accent, BackgroundColor = Palette.Header, MaxLength = 5 };
    private readonly Label documentTitle = new() { FontSize = 12, TextColor = Palette.Muted, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly Label sourceCaption = new() { FontSize = 11, TextColor = Palette.Muted, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly Label savedCaption = new() { FontSize = 11, TextColor = Palette.Muted };
    private readonly Label sizeCaption = new() { FontSize = 11, TextColor = Palette.Muted };
    private readonly Label modeCaption = new() { FontSize = 11, TextColor = Palette.Accent };
    private readonly Label cellCaption = new() { FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Palette.Accent };
    private readonly Label detail = new() { FontSize = 12, TextColor = Palette.Muted, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly Label status = new() { Text = "Готово", FontSize = 11, TextColor = Palette.Muted, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly Label driveCaption = new() { Text = "Google Drive не підключено", FontSize = 11, TextColor = Palette.Muted, MaxLines = 2 };
    private readonly Label libraryCaption = new() { Text = "Нещодавні документи", FontSize = 11, TextColor = Palette.Muted, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly ActivityIndicator activity = new() { Color = Palette.Accent, WidthRequest = 18, HeightRequest = 18 };
    private readonly VerticalStackLayout libraryItems = new() { Spacing = 5 };
    private Button undoButton = null!, redoButton = null!, modeButton = null!, syntaxButton = null!, referenceButton = null!, problemButton = null!;
    private Grid body = null!;
    private Border sidebar = null!;

    private void BuildLayout()
    {
        undoButton = ActionButton("↶", UndoAsync, hint: "Скасувати дію · Ctrl+Z");
        redoButton = ActionButton("↷", RedoAsync, hint: "Повторити дію · Ctrl+Y");
        modeButton = ActionButton("ƒx Формули", ToggleAsync);
        syntaxButton = ActionButton("Синтаксис", SyntaxAsync, hint: "Перевірити всі формули без обчислень");
        referenceButton = ActionButton("↗ Посилання", ReferenceAsync, hint: "Вставити адресу кліком по клітинці");
        problemButton = ActionButton("Без помилок", ProblemsAsync);
        var brand = new VerticalStackLayout { WidthRequest = 110, Spacing = 2, VerticalOptions = LayoutOptions.Center,
            Children = { new Label { Text = "Exel", TextColor = Palette.Accent, FontSize = 24, FontAttributes = FontAttributes.Bold }, documentTitle } };
        var toolbar = new HorizontalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center, Children = {
            ActionButton("＋ Нова", NewAsync, hint: "Новий документ · Ctrl+N"), ActionButton("Відкрити", OpenAsync, hint: "Відкрити · Ctrl+O"), ActionButton("Зберегти", SaveAsync, true, "Зберегти · Ctrl+S"),
            Divider(), undoButton, redoButton, Divider(), modeButton, syntaxButton, ActionButton("Обчислити", ForceCalculateAsync), Divider(), ActionButton("Drive", DriveAsync), ActionButton("?", HelpAsync, hint: "Довідка")
        }};
        var header = new Grid { Padding = new Thickness(18, 8), ColumnSpacing = 10, BackgroundColor = Palette.Background,
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)) };
        header.Add(brand); header.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = toolbar }, 1);
        var editor = new Grid { ColumnSpacing = 10, Padding = new Thickness(12, 6), BackgroundColor = Palette.Surface,
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)) };
        editor.Add(addressBox); editor.Add(new Label { Text = "ƒx", FontSize = 21, TextColor = Palette.Muted, VerticalTextAlignment = TextAlignment.Center }, 1);
        editor.Add(formula, 2); editor.Add(referenceButton, 3); editor.Add(ActionButton("✓", ApplyAsync, true, "Застосувати формулу · Enter"), 4);
        var structures = new HorizontalStackLayout { Spacing = 5, Children = {
            ActionButton("☰ Документи", SidebarAsync), Divider(),
            ActionButton("＋ Рядок", () => ResizeAsync(true, true)), ActionButton("− Рядок", () => ResizeAsync(true, false), hint: "Видалити останній рядок · можна повернути Undo"),
            ActionButton("＋ Стовпець", () => ResizeAsync(false, true)), ActionButton("− Стовпець", () => ResizeAsync(false, false), hint: "Видалити останній стовпець · можна повернути Undo"),
            Divider(), ActionButton("Очистити", ClearAsync), ActionButton("Повне значення", FullValueAsync), problemButton, ActionButton("Зупинити", () => Task.CompletedTask, hint: "Скасувати поточне обчислення", interrupt: true)
        }};
        sidebar = BuildSidebar();
        body = new Grid { ColumnSpacing = 10, Margin = new Thickness(14, 0), ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(206)), new ColumnDefinition(GridLength.Star)) };
        body.Add(sidebar); body.Add(new Border { BackgroundColor = Palette.Surface, Stroke = Palette.Line, StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = sheet, Padding = 0 }, 1);
        var inspection = new Grid { Padding = new Thickness(16, 7), RowSpacing = 3, RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)) };
        inspection.Add(cellCaption); inspection.Add(detail, 0, 1);
        var footer = new Grid { BackgroundColor = Palette.Surface, Padding = new Thickness(16, 7), ColumnSpacing = 18,
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        footer.Add(modeCaption); footer.Add(sizeCaption, 1); footer.Add(savedCaption, 2); footer.Add(status, 3); footer.Add(activity, 4);
        var root = new Grid { RowSpacing = 7, RowDefinitions = new RowDefinitionCollection(
            new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto),
            new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)) };
        root.Add(header, 0, 0); root.Add(new Border { Stroke = Palette.Line, StrokeThickness = 1, Content = editor, Margin = new Thickness(14, 0), StrokeShape = new RoundRectangle { CornerRadius = 7 } }, 0, 1);
        root.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Margin = new Thickness(14, 0), Content = structures }, 0, 2);
        root.Add(body, 0, 3); root.Add(inspection, 0, 4); root.Add(footer, 0, 5); Content = root;
    }
    private static BoxView Divider() => new() { WidthRequest = 1, HeightRequest = 23, Color = Palette.Line, Margin = new Thickness(5, 0), VerticalOptions = LayoutOptions.Center };
    private Button ActionButton(string text, Func<Task> action, bool primary = false, string? hint = null, bool interrupt = false)
    {
        var button = new Button { Text = text, FontSize = 12, FontAttributes = FontAttributes.Bold, HeightRequest = 35,
            Padding = new Thickness(11, 5), CornerRadius = 6, BackgroundColor = primary ? Palette.Accent : Palette.Header,
            TextColor = primary ? Palette.Background : Palette.Text, BorderColor = primary ? Palette.Accent : Palette.Line, BorderWidth = 1 };
        if (hint is not null) ToolTipProperties.SetText(button, hint);
        button.Pressed += async (_, _) => { sheet.ReleaseKeyboard(); button.Opacity = 0.76; await button.ScaleToAsync(0.96, 65, Easing.CubicOut); };
        button.Released += async (_, _) => { button.Opacity = 1; await button.ScaleToAsync(1, 125, Easing.CubicOut); };
        button.Clicked += (_, _) => { if (interrupt) calculation?.Cancel(); else Run(action); }; return button;
    }
    private Border BuildSidebar()
    {
        var samples = new VerticalStackLayout { Spacing = 5, Children = {
            LibraryButton("Великі числа", "BigInteger та степені", () => OpenSampleAsync("big-integers.isheet.json")),
            LibraryButton("Посилання", "Залежності між клітинками", () => OpenSampleAsync("references.isheet.json")),
            LibraryButton("Цикли й помилки", "Приклади діагностики", () => OpenSampleAsync("cycles.isheet.json"))
        }};
        var contents = new VerticalStackLayout { Spacing = 12, Children = {
            new Label { Text = "ДОКУМЕНТИ", FontSize = 10, CharacterSpacing = 1.7, TextColor = Palette.Muted, FontAttributes = FontAttributes.Bold },
            libraryCaption, libraryItems,
            ActionButton("Обрати папку…", PickFolderAsync),
            new BoxView { Color = Palette.Line, HeightRequest = 1, Margin = new Thickness(0, 4) },
            new Label { Text = "ПРИКЛАДИ", FontSize = 10, CharacterSpacing = 1.7, TextColor = Palette.Muted, FontAttributes = FontAttributes.Bold }, samples,
            new BoxView { Color = Palette.Line, HeightRequest = 1, Margin = new Thickness(0, 4) }, sourceCaption, driveCaption,
            new Label { Text = "F2 — редагувати\nCtrl+A — весь текст\nEnter / Tab — перейти далі", FontSize = 11, TextColor = Palette.Muted, LineHeight = 1.35 }
        }};
        return new Border { Content = new ScrollView { Content = contents }, Padding = 12, BackgroundColor = Palette.Surface, Stroke = Palette.Line, StrokeShape = new RoundRectangle { CornerRadius = 8 } };
    }
    private Border LibraryButton(string name, string subtitle, Func<Task> action)
    {
        var text = new VerticalStackLayout { Spacing = 3, Children = {
            new Label { Text = name, FontSize = 12, TextColor = Palette.Text, FontAttributes = FontAttributes.Bold, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation },
            new Label { Text = subtitle, FontSize = 10, TextColor = Palette.Muted, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation }
        }};
        var card = new Border { Content = text, Padding = new Thickness(10, 9), Stroke = Palette.Line, BackgroundColor = Palette.Header, StrokeShape = new RoundRectangle { CornerRadius = 6 } };
        var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => Run(action); card.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(card, name + ". " + subtitle);
        return card;
    }
    private async Task RefreshLibraryAsync()
    {
        libraryItems.Clear();
        string[] folderFiles = await documents.FolderFilesAsync();
        var files = documents.Folder is null ? documents.Recent() : folderFiles;
        libraryCaption.Text = documents.Folder is { } folder ? new DirectoryInfo(folder).Name : "Нещодавні документи";
        foreach (string path in files)
        {
            string captured = path;
            var card = LibraryButton(Path.GetFileNameWithoutExtension(path), File.GetLastWriteTime(path).ToString("dd.MM.yyyy · HH:mm"), () => LoadLocalAsync(captured));
            ToolTipProperties.SetText(card, path); libraryItems.Add(card);
        }
        if (!files.Any()) libraryItems.Add(new Label { Text = documents.Folder is null ? "Відкрийте або збережіть таблицю — вона з’явиться тут." : "У цій папці немає JSON-таблиць.", FontSize = 11, TextColor = Palette.Muted, LineHeight = 1.3 });
    }
    private Task SidebarAsync()
    {
        sidebar.IsVisible = !sidebar.IsVisible;
        body.ColumnDefinitions[0].Width = sidebar.IsVisible ? new GridLength(206) : new GridLength(0);
        body.ColumnSpacing = sidebar.IsVisible ? 10 : 0;
        return Task.CompletedTask;
    }
    private void BuildMenus()
    {
        var file = new MenuBarItem { Text = "Файл" };
        file.Add(Menu("Нова таблиця    Ctrl+N", NewAsync)); file.Add(Menu("Відкрити…    Ctrl+O", OpenAsync));
        file.Add(Menu("Зберегти    Ctrl+S", SaveAsync)); file.Add(Menu("Зберегти як…", SaveAsAsync)); file.Add(Menu("Назва документа…", RenameAsync));
        var edit = new MenuBarItem { Text = "Редагування" };
        edit.Add(Menu("Скасувати    Ctrl+Z", UndoAsync)); edit.Add(Menu("Повторити    Ctrl+Y", RedoAsync));
        edit.Add(Menu("Очистити клітинку    Delete", ClearAsync)); edit.Add(Menu("Вставити посилання кліком", ReferenceAsync));
        var table = new MenuBarItem { Text = "Таблиця" };
        table.Add(Menu("Додати рядок унизу", () => ResizeAsync(true, true))); table.Add(Menu("Прибрати останній рядок", () => ResizeAsync(true, false)));
        table.Add(Menu("Додати стовпець праворуч", () => ResizeAsync(false, true))); table.Add(Menu("Прибрати останній стовпець", () => ResizeAsync(false, false)));
        var calculationMenu = new MenuBarItem { Text = "Обчислення" };
        calculationMenu.Add(Menu("Перевірити синтаксис", SyntaxAsync)); calculationMenu.Add(Menu("Обчислити все", ForceCalculateAsync)); calculationMenu.Add(Menu("Показати помилки", ProblemsAsync));
        var view = new MenuBarItem { Text = "Вигляд" };
        view.Add(Menu("Формули / значення", ToggleAsync)); view.Add(Menu("Панель документів", SidebarAsync)); view.Add(Menu("Повне значення клітинки", FullValueAsync));
        var cloud = new MenuBarItem { Text = "Google Drive" }; cloud.Add(Menu("Підключення та файли…", DriveAsync));
        var help = new MenuBarItem { Text = "Допомога" }; help.Add(Menu("Як користуватися", HelpAsync)); help.Add(Menu("Посилання на клітинки", ReferencesHelpAsync)); help.Add(Menu("Про Exel", AboutAsync));
        foreach (var menu in new[] { file, edit, table, calculationMenu, view, cloud, help }) MenuBarItems.Add(menu);
    }
    private MenuFlyoutItem Menu(string text, Func<Task> action) => new() { Text = text, Command = new Command(() => Run(action)) };
    private Task HelpAsync() => ShowTextAsync("Довідка Exel",
        "Виберіть клітинку. Введіть число або формулу у рядку ƒx; подвійний клік чи F2 відкриває редактор у самій клітинці. Ctrl+A виділяє весь текст.\n\nEnter застосовує формулу й переходить на рядок нижче, Tab — праворуч. Escape скасовує незастосоване редагування. Стрілки переміщують вибір; усередині редактора вони переміщують курсор.\n\nФормули: 5^8, =max(A1,B2), 17div5, A1mod3. Початковий = необов’язковий. Від’ємне число: (0-5). / і div — цілочисельне ділення.\n\n«↗ Посилання»: натисніть кнопку, потім потрібну клітинку. Її адресу буде вставлено у формулу, яку ви редагуєте.\n\n«Синтаксис» перевіряє правильність запису без обчислення. «Обчислити» перевіряє значення, посилання та цикли.\n\nUndo/Redo повертають до 40 застосованих змін. Видалення рядка/стовпця прибирає останній; Undo відновлює його разом із даними.\n\nКолесо прокручує рядки, Shift+колесо — стовпці. Тонкі смуги біля таблиці можна перетягувати. Введіть адресу у поле ліворуч від ƒx і натисніть Enter для переходу.\n\nCtrl+N — новий документ, Ctrl+O — відкрити, Ctrl+S — зберегти, Ctrl+Z / Ctrl+Y — історія. Файли мають власний формат JSON, не XLSX. Автозбереження немає.");
    private Task ReferencesHelpAsync() => ShowTextAsync("Посилання й цикли",
        "1. У A1 введіть 10.\n2. У B1 введіть =A1*2. Значення B1 — 20.\n3. Змініть A1 на 15 і застосуйте: B1 стане 30.\n\nДля вставлення адреси мишею: почніть формулу, натисніть «↗ Посилання» та клацніть потрібну клітинку. Продовжуйте введення у початковій клітинці.\n\nУсі адреси — латиницею, від A1 до AZ200. Порожнє посилання дорівнює нулю.\n\nЯкщо A1 містить B1+1, а B1 містить A1+1, з’явиться #CYCLE. Змініть одну з цих формул на число, щоб розірвати цикл.\n\nПриклади ліворуч можна відкрити без завантаження файлів.");
    private Task AboutAsync() => DisplayAlertAsync("Exel", "Електронні таблиці для лабораторної № 1, варіант 6.\n\nC# · .NET MAUI 10 · Windows\nBigInteger · набори операцій 1, 2, 4, 6.\n\nВласний табличний редактор із локальними файлами та Google Drive.", "Закрити");
    private async Task ShowTextAsync(string title, string text)
    {
        var value = new Editor { Text = text, IsReadOnly = true, FontFamily = "Consolas", FontSize = 14, TextColor = Palette.Text, BackgroundColor = Palette.Surface };
        var page = new ContentPage { Title = "", BackgroundColor = Palette.Background };
        var close = new Button { Text = "Повернутися", BackgroundColor = Palette.Accent, TextColor = Palette.Background, CornerRadius = 6 };
        var copy = new Button { Text = "Копіювати текст", BackgroundColor = Palette.Header, TextColor = Palette.Text, CornerRadius = 6 };
        var grid = new Grid { Padding = 22, RowSpacing = 15, RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)) };
        grid.Add(new Label { Text = title, FontSize = 23, TextColor = Palette.Text, FontAttributes = FontAttributes.Bold }); grid.Add(value, 0, 1);
        grid.Add(new HorizontalStackLayout { Spacing = 10, Children = { close, copy } }, 0, 2);
        close.Clicked += async (_, _) => await Navigation.PopModalAsync(); copy.Clicked += async (_, _) => await Clipboard.Default.SetTextAsync(text);
        page.Content = grid; await Navigation.PushModalAsync(page);
    }
}
