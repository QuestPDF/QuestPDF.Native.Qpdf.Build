using System.Runtime.InteropServices;

Environment.CurrentDirectory = AppContext.BaseDirectory;

var libraryVersion = API.GetQpdfVersion();
Console.WriteLine(libraryVersion);

API.qpdf_init();

RunTest("page_selection_job.json");
RunTest("password_job.json");
RunTest("attachment_job.json");

static void RunTest(string jobFile)
{
    var job = File.ReadAllText(jobFile);
    
    var metadata = File.ReadAllText("zugfred_metadata.txt");
    metadata = metadata.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    job = job.Replace("{{metadata}}", metadata);

    var jobHandle = API.qpdfjob_init();
    API.qpdfjob_initialize_from_json(jobHandle, job);
    API.qpdfjob_run(jobHandle);
    API.qpdfjob_cleanup(ref jobHandle);
}

static class API
{
    const string LibraryName = "qpdf";
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr qpdf_init();
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr qpdf_get_qpdf_version();
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr qpdfjob_init();
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void qpdfjob_cleanup(ref IntPtr jobHandle);
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int qpdfjob_initialize_from_json(IntPtr jobHandle, string json);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int qpdfjob_run(IntPtr jobHandle);
    
    public static string GetQpdfVersion()
    {
        var ptr = qpdf_get_qpdf_version();
        return Marshal.PtrToStringAnsi(ptr);
    }
}
