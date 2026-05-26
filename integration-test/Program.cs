using QuestPDF.Qpdf;

Environment.CurrentDirectory = AppContext.BaseDirectory;

var libraryVersion = QpdfAPI.GetQpdfVersion();
Console.WriteLine(libraryVersion);

RunTest("page_selection_job.json");
RunTest("password_job.json");
RunTest("attachment_job.json");

static void RunTest(string jobFile)
{
    Console.WriteLine("Running test: " + jobFile);
    
    var job = File.ReadAllText(jobFile);
    
    var metadata = File.ReadAllText("zugfred_metadata.txt");
    metadata = metadata.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    job = job.Replace("{{metadata}}", metadata);

    QpdfAPI.ExecuteJob(job);
}
