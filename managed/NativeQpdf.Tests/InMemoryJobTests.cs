using System.Text.Json.Nodes;
using NativeQpdf.Tests.Helpers;
using NUnit.Framework;
using QuestPDF.Qpdf;

namespace NativeQpdf.Tests;

// Runs the same jobs as FileJobTests, with all input files and the output file in memory.
public class InMemoryJobTests
{
    private const string DocumentBufferName = "document";

    [Test]
    public void PageSelection()
    {
        var output = ExecuteAndCompareWithFileOutput("page_selection_job.json");
        PdfValidator.Validate(output);
    }

    [Test]
    public void Password()
    {
        var (job, inputBuffers) = ConvertToInMemoryJob(TestFiles.ReadJob("password_job.json"));
        var output = JobRunner.ExecuteInMemory(job, inputBuffers);

        PdfValidator.AssertRequiresPassword(output);
        PdfValidator.Validate(output, "user_password");
    }

    [Test]
    public void AttachmentWithMetadata()
    {
        var output = ExecuteAndCompareWithFileOutput("attachment_job.json", ZugferdMetadata.AddTo);
        PdfValidator.Validate(output);
    }
    
    [Test]
    public void FailingOutputStream()
    {
        var document = TestFiles.ReadBytes("input_normal.pdf");
        var job = CreateDocumentJob();

        var exception = Assert.Throws<IOException>(() =>
        {
            QpdfAPI.ExecuteJob(
                job.ToJsonString(), 
                inputBuffers: CreateDocumentBuffers(document),
                outputStream: new FailingStream());
        });

        Assert.That(exception.Message, Is.EqualTo(FailingStream.ErrorMessage));
    }

    [Test]
    public void UnregisteredInputBuffer()
    {
        var job = CreateDocumentJob();

        var exception = Assert.Throws<Exception>(() =>
        {
            JobRunner.ExecuteInMemory(job, new Dictionary<string, byte[]>());
        });
        
        Assert.That(exception.Message, Does.Contain(job["inputFile"]!.GetValue<string>()));
    }
    
    private static byte[] ExecuteAndCompareWithFileOutput(string jobFile, Func<string?, string>? transformMetadata = null)
    {
        var fileJob = TestFiles.ReadJob(jobFile);
        MakeOutputDeterministic(fileJob);

        var fileOutputName = Path.GetFileName(fileJob["outputFile"]!.GetValue<string>());
        fileJob["outputFile"] = Path.Combine(TestFiles.OutputPath, "in_memory_comparison_" + fileOutputName);

        var (memoryJob, inputBuffers) = ConvertToInMemoryJob(fileJob);

        var fileOutput = JobRunner.ExecuteWithFiles(fileJob, transformMetadata);
        var memoryOutput = JobRunner.ExecuteInMemory(memoryJob, inputBuffers, transformMetadata);

        Assert.That(memoryOutput, Is.EqualTo(fileOutput), "The in-memory output is different from the output written to a file");
        return memoryOutput;
    }

    // qpdf uses the current time for the document ID and the attachment dates, unless configured otherwise
    private static void MakeOutputDeterministic(JsonObject job)
    {
        job["deterministicId"] = "";

        foreach (var attachment in job["addAttachment"]?.AsArray() ?? [])
        {
            attachment!["creationdate"] = "D:20240101000000Z";
            attachment["moddate"] = "D:20240101000000Z";
        }
    }

    private static (JsonObject Job, Dictionary<string, byte[]> InputBuffers) ConvertToInMemoryJob(JsonObject fileJob)
    {
        var job = fileJob.DeepClone().AsObject();
        var inputBuffers = new Dictionary<string, byte[]>();

        UseInputBuffer(job, "inputFile");

        foreach (var page in job["pages"]?.AsArray() ?? [])
        {
            // "." refers to the main input file
            if (page!["file"]!.GetValue<string>() != ".")
                UseInputBuffer(page, "file");
        }

        foreach (var attachment in job["addAttachment"]?.AsArray() ?? [])
            UseInputBuffer(attachment!, "file");

        job["outputFile"] = QpdfAPI.OutputBufferReference;
        return (job, inputBuffers);

        // each input file is registered under its own file name, so that names derived from it (such as attachment keys) do not change
        void UseInputBuffer(JsonNode node, string property)
        {
            var filePath = node[property]!.GetValue<string>();
            var fileName = Path.GetFileName(filePath);

            inputBuffers[fileName] = File.ReadAllBytes(filePath);
            node[property] = QpdfAPI.BufferReferencePrefix + fileName;
        }
    }

    private static JsonObject CreateDocumentJob()
    {
        return new JsonObject
        {
            ["inputFile"] = QpdfAPI.BufferReferencePrefix + DocumentBufferName,
            ["outputFile"] = QpdfAPI.OutputBufferReference
        };
    }

    private static Dictionary<string, byte[]> CreateDocumentBuffers(byte[] document)
    {
        return new Dictionary<string, byte[]> { [DocumentBufferName] = document };
    }
}
