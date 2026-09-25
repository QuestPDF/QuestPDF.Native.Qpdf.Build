using QuestPDF.Qpdf;

Environment.CurrentDirectory = AppContext.BaseDirectory;

if(!QpdfAPI.IsCorrectVersionLoaded())
    throw new Exception("Incorrect version of the library loaded");

RunTest("page_selection_job.json");
RunTest("password_job.json");
RunTest("attachment_job.json", ExtendMetadata);

static void RunTest(string jobFile, Func<string?, string>? transformMetadata = null)
{
    Console.WriteLine("Running test: " + jobFile);

    var job = File.ReadAllText(jobFile);
    QpdfAPI.ExecuteJob(job, transformMetadata);
}

// inserts additional RDF descriptions into the existing XMP metadata of the document
static string ExtendMetadata(string? metadata)
{
    if (metadata == null)
        throw new Exception("The document does not contain XMP metadata to extend");

    const string rdfClosingTag = "</rdf:RDF>";
    var insertionIndex = metadata.LastIndexOf(rdfClosingTag, StringComparison.Ordinal);

    if (insertionIndex < 0)
        throw new Exception($"The XMP metadata does not contain the {rdfClosingTag} tag");

    var extension = File.ReadAllText("zugfred_metadata.txt");
    return metadata.Insert(insertionIndex, extension);
}
