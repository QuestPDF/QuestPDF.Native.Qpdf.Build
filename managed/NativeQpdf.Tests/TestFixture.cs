using NativeQpdf.Tests.Helpers;
using NUnit.Framework;
using QuestPDF.Qpdf;

namespace NativeQpdf.Tests;

[SetUpFixture]
public class TestFixture
{
    [OneTimeSetUp]
    public void SetUp()
    {
        Assert.That(QpdfAPI.IsCorrectVersionLoaded(), Is.True, "Incorrect version of the native library loaded");

        if (Directory.Exists(TestFiles.OutputPath))
            Directory.Delete(TestFiles.OutputPath, true);

        Directory.CreateDirectory(TestFiles.OutputPath);
    }
}
