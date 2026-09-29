using NativeQpdf.Tests.Helpers;
using NUnit.Framework;

namespace NativeQpdf.Tests;

public class FileJobTests
{
    [Test]
    public void PageSelection()
    {
        var output = ExecuteJob("page_selection_job.json");

        var input = PdfValidator.Validate(TestFiles.ReadBytes("input_normal.pdf"));
        var document = PdfValidator.Validate(output);

        Assert.That(document.PageTexts, Is.EqualTo(input.PageTexts.Skip(1).Take(3)), "The document must contain pages 2-4 of the input");
    }

    [Test]
    public void Password()
    {
        var output = ExecuteJob("password_job.json");

        PdfValidator.AssertRequiresPassword(output);
        PdfValidator.Validate(output, "user_password");
        PdfValidator.Validate(output, "owner_password");
    }

    [Test]
    public void AttachmentWithMetadata()
    {
        var output = ExecuteJob("attachment_job.json", ZugferdMetadata.AddTo);
        var document = PdfValidator.Validate(output);

        Assert.That(document.Attachments.GetValueOrDefault("factur-x.xml"), Is.EqualTo(TestFiles.ReadBytes("zugfred.xml")), "The document must contain the invoice attachment");
        Assert.That(document.XmpMetadata, Does.Contain(ZugferdMetadata.Extension));
    }

    private static byte[] ExecuteJob(string jobFile, Func<string?, string>? transformMetadata = null)
    {
        return JobRunner.ExecuteWithFiles(TestFiles.ReadJob(jobFile), transformMetadata);
    }
}
