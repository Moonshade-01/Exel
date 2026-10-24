using Microsoft.Windows.Storage.Pickers;
namespace Exel.Services;

internal static class NativeFileDialogs
{
    private static Microsoft.UI.WindowId Owner(Page page) =>
        (page.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window)?.AppWindow.Id
        ?? throw new InvalidOperationException("Вікно ще не готове.");
    public static async Task<string?> SaveAsync(Page page, string name, string? folder)
    {
        string clean = string.Concat(name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var picker = new FileSavePicker(Owner(page))
        {
            SuggestedFileName = clean + ".isheet",
            DefaultFileExtension = ".json",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            CommitButtonText = "Зберегти таблицю"
        };
        picker.FileTypeChoices.Add("Таблиця Exel", new List<string> { ".json" });
        if (folder is not null && Directory.Exists(folder)) picker.SuggestedFolder = folder;
        return (await picker.PickSaveFileAsync())?.Path;
    }
    public static async Task<string?> FolderAsync(Page page)
    {
        var picker = new FolderPicker(Owner(page)) { CommitButtonText = "Обрати папку" };
        return (await picker.PickSingleFolderAsync())?.Path;
    }
}
