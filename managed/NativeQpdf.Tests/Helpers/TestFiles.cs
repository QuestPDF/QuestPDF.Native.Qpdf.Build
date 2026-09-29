using System.Text.Json.Nodes;

namespace NativeQpdf.Tests.Helpers;

// The input files used by the tests, and the directory for the files produced by them.
internal static class TestFiles
{
    public const string InputPath = "Input";
    public const string OutputPath = "Output";

    public static byte[] ReadBytes(string fileName)
    {
        return File.ReadAllBytes(Path.Combine(InputPath, fileName));
    }

    public static string ReadText(string fileName)
    {
        return File.ReadAllText(Path.Combine(InputPath, fileName));
    }

    public static JsonObject ReadJob(string fileName)
    {
        return JsonNode.Parse(ReadText(fileName))!.AsObject();
    }
}
