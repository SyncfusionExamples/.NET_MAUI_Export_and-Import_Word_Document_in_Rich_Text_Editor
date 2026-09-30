using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.Maui.RichTextEditor;
using System.Text.RegularExpressions;
using CommunityToolkit.Maui.Storage;

namespace Rich_Text_Editor_Import_and_Export;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
        var file = await FilePicker.Default.PickAsync(
            new PickOptions { PickerTitle = "Select a Word Document" });

        if (file == null || !file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlertAsync("Invalid File", "Please select a .docx file.", "OK");
            return;
        }

        using var inStream = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await inStream.CopyToAsync(ms);
        ms.Position = 0;

        using var document = new WordDocument(ms, FormatType.Automatic);

        // Inline images as base64 during HTML export
        document.SaveOptions.ImageNodeVisited += (s, args) =>
        {
            using var imgMs = new MemoryStream();
            args.ImageStream.Position = 0;
            args.ImageStream.CopyTo(imgMs);

            var bytes = imgMs.ToArray();
            string mime =
                bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 ? "image/jpeg" :
                bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 ? "image/png" :
                bytes.Length > 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 ? "image/gif" :
                bytes.Length > 2 && bytes[0] == 0x42 && bytes[1] == 0x4D ? "image/bmp" :
                "image/png";

            var b64 = Convert.ToBase64String(bytes);
            args.Uri = $"data:{mime};base64,{b64}";
        };

        using var htmlStream = new MemoryStream();
        document.Save(htmlStream, FormatType.Html);
        htmlStream.Position = 0;

        using var reader = new StreamReader(htmlStream);
        var html = await reader.ReadToEndAsync();

        richTextEditor.Value = html;
    }

    private async void OnImageInserting(object sender, RichTextEditorImageRequestedEventArgs e)
    {
        e.IsHandled = true;
        RichTextEditorImageSource richTextEditorImageSource = new();
        richTextEditorImageSource.ImageFormat = RichTextEditorImageFormat.Base64;
        richTextEditorImageSource.Source = ImageSource.FromUri(new Uri("https://aka.ms/campus.jpg"));
        richTextEditorImageSource.Width = 500;
        richTextEditorImageSource.Height = 200;
        richTextEditor.InsertImage(richTextEditorImageSource);
    }

    private async void OnExport(object sender, EventArgs e)
    {
        var html = PrepareHtml((string)richTextEditor.Value!);

        using var document = new WordDocument();
        document.EnsureMinimal();
        document.HTMLImportSettings.ImageNodeVisited += OnImageNodeVisited;
        document.AddSection().Body.InsertXHTML(html);
        document.HTMLImportSettings.ImageNodeVisited -= OnImageNodeVisited;

        using var output = new MemoryStream();
        document.Save(output, FormatType.Docx);
        document.Close();
        output.Position = 0;

        // Shows the OS save dialog so the user can rename and choose the location.
        var saveResult = await FileSaver.Default.SaveAsync("ExportedDocument.docx", output, CancellationToken.None);

        if (!saveResult.IsSuccessful && saveResult.Exception is not null)
            await DisplayAlertAsync("Export failed", saveResult.Exception.Message, "OK");
    }

    private static string PrepareHtml(string html) =>
        html.IndexOf('\\') >= 0
            ? Regex.Unescape(html).Trim('"')
            : html;

    private static void OnImageNodeVisited(object sender, ImageNodeVisitedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Uri) ||
            !args.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return;

        var stream = LoadDataUri(args.Uri);
        if (stream != null)
            args.ImageStream = stream;
    }

    private static Stream? LoadDataUri(string uri)
    {
        var commaIndex = uri.IndexOf(',');
        if (commaIndex < 0)
            return null;

        var base64 = uri[(commaIndex + 1)..]
            .Trim()
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Replace(" ", string.Empty);

        try
        {
            return new MemoryStream(Convert.FromBase64String(base64));
        }
        catch
        {
            return null;
        }
    }

    private void OnClear(object sender, EventArgs e)
    {
        richTextEditor.Value = string.Empty;
    }
}