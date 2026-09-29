namespace NativeQpdf.Tests.Helpers;

// The Factur-X / ZUGFeRD RDF descriptions, added by the attachment job to the XMP metadata of the document.
internal static class ZugferdMetadata
{
    public static string Extension => TestFiles.ReadText("zugfred_metadata.txt");

    // Inserts the RDF descriptions into the existing XMP metadata of the document.
    public static string AddTo(string? metadata)
    {
        if (metadata == null)
            throw new Exception("The document does not contain XMP metadata to extend");

        const string rdfClosingTag = "</rdf:RDF>";
        var insertionIndex = metadata.LastIndexOf(rdfClosingTag, StringComparison.Ordinal);

        if (insertionIndex < 0)
            throw new Exception($"The XMP metadata does not contain the {rdfClosingTag} tag");

        return metadata.Insert(insertionIndex, Extension);
    }
}
