using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using MigraDoc.Rendering;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace TouchSpeak.Services;

/// <summary>
/// Import / export between the editor and files.
/// RTF keeps full formatting (native WPF). TXT/DOCX/PDF are text-level.
/// </summary>
public static class DocumentIoService
{
    public const string ImportFilter =
        "Alle unterst\u00fctzten|*.txt;*.rtf;*.docx|Text (*.txt)|*.txt|Rich Text (*.rtf)|*.rtf|Word (*.docx)|*.docx";

    public const string ExportFilter =
        "Text (*.txt)|*.txt|Rich Text (*.rtf)|*.rtf|Word (*.docx)|*.docx|PDF (*.pdf)|*.pdf";

    // ---------- Import ----------

    public static void Load(RichTextBox editor, string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".rtf":
                LoadRtf(editor, path);
                break;
            case ".docx":
                SetPlainText(editor, ReadDocx(path));
                break;
            default: // .txt and anything else
                SetPlainText(editor, File.ReadAllText(path, Encoding.UTF8));
                break;
        }
    }

    private static void LoadRtf(RichTextBox editor, string path)
    {
        var doc = editor.Document;
        var range = new TextRange(doc.ContentStart, doc.ContentEnd);
        using var fs = File.OpenRead(path);
        range.Load(fs, DataFormats.Rtf);
    }

    private static void SetPlainText(RichTextBox editor, string text)
    {
        var doc = editor.Document;
        doc.Blocks.Clear();
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            doc.Blocks.Add(new Paragraph(new Run(line)));
    }

    private static string ReadDocx(string path)
    {
        var sb = new StringBuilder();
        using var word = WordprocessingDocument.Open(path, false);
        var body = word.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;

        foreach (var para in body.Elements<W.Paragraph>())
        {
            var text = string.Concat(para.Descendants<W.Text>().Select(t => t.Text));
            sb.AppendLine(text);
        }
        return sb.ToString().TrimEnd('\n', '\r');
    }

    // ---------- Export ----------

    public static void Save(RichTextBox editor, string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".rtf":
                SaveRtf(editor, path);
                break;
            case ".docx":
                WriteDocx(path, GetPlainText(editor));
                break;
            case ".pdf":
                WritePdf(path, GetPlainText(editor));
                break;
            default: // .txt
                File.WriteAllText(path, GetPlainText(editor), Encoding.UTF8);
                break;
        }
    }

    public static string GetPlainText(RichTextBox editor)
    {
        var doc = editor.Document;
        return new TextRange(doc.ContentStart, doc.ContentEnd).Text;
    }

    private static void SaveRtf(RichTextBox editor, string path)
    {
        var doc = editor.Document;
        var range = new TextRange(doc.ContentStart, doc.ContentEnd);
        using var fs = File.Create(path);
        range.Save(fs, DataFormats.Rtf);
    }

    private static void WriteDocx(string path, string text)
    {
        using var word = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = word.AddMainDocumentPart();
        main.Document = new W.Document();
        var body = main.Document.AppendChild(new W.Body());

        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var run = new W.Run(new W.Text(line) { Space = SpaceProcessingModeValues.Preserve });
            body.AppendChild(new W.Paragraph(run));
        }
    }

    private static void WritePdf(string path, string text)
    {
        var doc = new MigraDoc.DocumentObjectModel.Document();
        var section = doc.AddSection();
        section.PageSetup.LeftMargin = "2.5cm";
        section.PageSetup.RightMargin = "2.5cm";
        section.PageSetup.TopMargin = "2cm";

        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var p = section.AddParagraph(string.IsNullOrEmpty(line) ? " " : line);
            p.Format.Font.Name = "Arial";
            p.Format.Font.Size = 12;
            p.Format.SpaceAfter = "6pt";
        }

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }
}
