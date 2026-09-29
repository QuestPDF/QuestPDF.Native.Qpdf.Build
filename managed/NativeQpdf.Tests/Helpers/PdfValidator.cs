using System.Text;
using NUnit.Framework;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace NativeQpdf.Tests.Helpers;

// The content of a validated document, for the tests to check.
internal sealed record ValidatedDocument(
    IReadOnlyList<string> PageTexts,
    IReadOnlyDictionary<string, byte[]> Attachments,
    string? XmpMetadata);

// Validates documents with PdfPig, a PDF library independent of qpdf,
// so that a broken native build cannot confirm the correctness of its own output.
internal static class PdfValidator
{
    // Parses the whole document without tolerating errors, which fails when the document is damaged.
    public static ValidatedDocument Validate(byte[] document, string? password = null)
    {
        var options = new ParsingOptions
        {
            UseLenientParsing = false,
            Password = password ?? string.Empty
        };

        using var pdf = PdfDocument.Open(document, options);

        // extracting the text parses the content of every page
        var pageTexts = pdf.GetPages().Select(page => page.Text).ToList();

        // attachments are identified by their file names
        var attachments = pdf.Advanced.TryGetEmbeddedFiles(out var embeddedFiles)
            ? embeddedFiles.ToDictionary(file => file.FileSpecification, file => file.Memory.ToArray())
            : new Dictionary<string, byte[]>();

        var xmpMetadata = pdf.TryGetXmpMetadata(out var metadata)
            ? Encoding.UTF8.GetString(metadata.GetXmlBytes())
            : null;

        return new ValidatedDocument(pageTexts, attachments, xmpMetadata);
    }

    public static void AssertRequiresPassword(byte[] document)
    {
        Assert.Throws<PdfDocumentEncryptedException>(() => Validate(document), "The document can be opened without a password");
    }
}
