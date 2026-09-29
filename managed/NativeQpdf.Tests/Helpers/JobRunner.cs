using System.Text.Json.Nodes;
using QuestPDF.Qpdf;

namespace NativeQpdf.Tests.Helpers;

internal static class JobRunner
{
    // Runs the job reading and writing files, and returns the produced document.
    public static byte[] ExecuteWithFiles(JsonObject job, Func<string?, string>? transformMetadata = null)
    {
        QpdfAPI.ExecuteJob(job.ToJsonString(), transformMetadata);
        return File.ReadAllBytes(job["outputFile"]!.GetValue<string>());
    }

    // Runs the job with in-memory input buffers, and returns the document written to the output stream.
    public static byte[] ExecuteInMemory(JsonObject job, IReadOnlyDictionary<string, byte[]> inputBuffers, Func<string?, string>? transformMetadata = null)
    {
        using var outputStream = new MemoryStream();
        QpdfAPI.ExecuteJob(job.ToJsonString(), transformMetadata, inputBuffers, outputStream);
        return outputStream.ToArray();
    }
}
