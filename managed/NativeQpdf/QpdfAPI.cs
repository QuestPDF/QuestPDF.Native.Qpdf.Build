using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace QuestPDF.Qpdf;

static class QpdfAPI
{
    const int ExpectedNativeLibraryVersion = 5;

    /// <summary>
    /// The job configuration references in-memory buffers with this prefix in place of file paths.
    /// </summary>
    public const string BufferReferencePrefix = "qpdf-buffer://";

    private const string OutputBufferName = "output";
    public const string OutputBufferReference = BufferReferencePrefix + OutputBufferName;

    public static bool IsCorrectVersionLoaded()
    {
        try
        {
            return API.questpdf_get_compatibility_version() == ExpectedNativeLibraryVersion;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs the job in two stages: loading the document with all job operations applied, and writing it.
    /// When provided, <paramref name="transformMetadata"/> is called in between: it receives the current XMP metadata
    /// (null when the document has none) and returns the XMP metadata to store.
    /// The job configuration may reference <paramref name="inputBuffers"/> by name (with the <see cref="BufferReferencePrefix"/>),
    /// and write its output to <paramref name="outputStream"/> by using <see cref="OutputBufferReference"/> as the output file.
    /// </summary>
    public static void ExecuteJob(
        string jobJson,
        Func<string?, string>? transformMetadata = null,
        IReadOnlyDictionary<string, byte[]>? inputBuffers = null,
        Stream? outputStream = null)
    {
        // create StringBuilder that will store the error message
        var error = new StringBuilder();
        var errorHandle = GCHandle.Alloc(error);
        var errorPtr = GCHandle.ToIntPtr(errorHandle);
        
        var logger = IntPtr.Zero;
        var jobHandle = IntPtr.Zero;
        var documentHandle = IntPtr.Zero;

        var inputHandles = new List<GCHandle>();
        var output = outputStream != null ? new OutputStreamWriter(outputStream) : null;
        var outputHandle = default(GCHandle);
        
        try
        {
            // create logger
            logger = API.qpdflogger_create();
            API.qpdflogger_set_error(logger, 4, LoggingCallbackPointer, errorPtr); // 4 = custom logger

            // prepare the job
            jobHandle = API.qpdfjob_init();
            API.qpdfjob_set_logger(jobHandle, logger);
            RegisterInputBuffers();
            RegisterOutputBuffer();
            ThrowOnJobError(API.qpdfjob_initialize_from_json(jobHandle, jobJson));

            // stage 1: load the document and apply all job operations
            documentHandle = API.qpdfjob_create_qpdf(jobHandle);

            if (documentHandle == IntPtr.Zero)
                throw CreateJobException();

            if (transformMetadata != null)
            {
                ThrowOnJobError(API.questpdf_job_get_xmp_metadata(jobHandle, documentHandle, out var buffer, out var length));
                var metadata = ReadBufferAsStringAndFree(buffer, length);

                // qpdf copies the content, so the managed array is needed only for the duration of the call
                var newMetadata = transformMetadata(metadata);
                var newContent = Encoding.UTF8.GetBytes(newMetadata);
                ThrowOnJobError(API.questpdf_job_set_xmp_metadata(jobHandle, documentHandle, newContent, (UIntPtr)newContent.Length));
            }

            // stage 2: write the document
            ThrowOnJobError(API.qpdfjob_write_qpdf(jobHandle, documentHandle));
        }
        finally
        {
            if (documentHandle != IntPtr.Zero)
                API.qpdf_cleanup(ref documentHandle);

            if (jobHandle != IntPtr.Zero)
                API.qpdfjob_cleanup(ref jobHandle);

            if (logger != IntPtr.Zero)
                API.qpdflogger_cleanup(ref logger);

            // qpdf reads the input buffers lazily and keeps pointers to them until the clean-up above
            foreach (var inputHandle in inputHandles)
                inputHandle.Free();

            if (outputHandle.IsAllocated)
                outputHandle.Free();

            errorHandle.Free();
        }

        void RegisterInputBuffers()
        {
            foreach (var inputBuffer in inputBuffers ?? new Dictionary<string, byte[]>())
            {
                var inputHandle = GCHandle.Alloc(inputBuffer.Value, GCHandleType.Pinned);
                inputHandles.Add(inputHandle);

                ThrowOnJobError(API.qpdfjob_register_buffer_input(jobHandle, inputBuffer.Key, inputHandle.AddrOfPinnedObject(), (UIntPtr)inputBuffer.Value.Length));
            }
        }
        
        void RegisterOutputBuffer()
        {
            if (output == null)
                return;

            outputHandle = GCHandle.Alloc(output);
            ThrowOnJobError(API.qpdfjob_register_buffer_output(jobHandle, OutputBufferName, OutputCallbackPointer, GCHandle.ToIntPtr(outputHandle)));
        }

        void ThrowOnJobError(int jobResultId)
        {
            output?.WriteException?.Throw();

            // 0 = success, 1 = undefined, 2 = error, 3 = warning
            if (jobResultId is not 2)
                return;

            throw CreateJobException();
        }

        Exception CreateJobException() => new Exception($"QuestPDF could not perform document operation:\n\n{error}");
    }

    private static string? ReadBufferAsStringAndFree(IntPtr buffer, UIntPtr length)
    {
        if (buffer == IntPtr.Zero)
            return null;

        try
        {
            var content = new byte[(int)length];
            Marshal.Copy(buffer, content, 0, content.Length);
            return Encoding.UTF8.GetString(content);
        }
        finally
        {
            API.qpdf_oh_free_buffer(ref buffer);
        }
    }
    
    #region Output Stream
    
    private sealed class OutputStreamWriter(Stream targetStream)
    {
        public ExceptionDispatchInfo? WriteException { get; private set; }

        public int Write(IntPtr data, UIntPtr length)
        {
            try
            {
                var size = checked((int)length.ToUInt64());

                unsafe
                {
                    targetStream.Write(new ReadOnlySpan<byte>((void*)data, size));
                }

                return 0;
            }
            catch (Exception exception)
            {
                WriteException = ExceptionDispatchInfo.Capture(exception);
                return 1; // any non-zero value aborts the job
            }
        }
    }

    private static int OutputCallback(IntPtr data, UIntPtr length, IntPtr udata)
    {
        var handle = GCHandle.FromIntPtr(udata);
        var writer = (OutputStreamWriter)handle.Target!;
        return writer.Write(data, length);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OutputCallbackDelegate(IntPtr data, UIntPtr length, IntPtr udata);

    private static readonly OutputCallbackDelegate OutputCallbackDelegateInstance = OutputCallback;

    private static readonly IntPtr OutputCallbackPointer = Marshal.GetFunctionPointerForDelegate(OutputCallbackDelegateInstance);

    #endregion

    #region Logging

    private static int LoggingCallback(IntPtr data, int length, IntPtr udata)
    {
        var bytes = new byte[length];
        Marshal.Copy(data, bytes, 0, length);

        var handle = GCHandle.FromIntPtr(udata);
        var stringBuilder = (StringBuilder)handle.Target;
        stringBuilder?.Append(Encoding.UTF8.GetString(bytes));

        return 0;
    }
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CallbackDelegate(IntPtr data, int length, IntPtr udata);
    
    private static readonly CallbackDelegate LoggingCallbackDelegate = LoggingCallback;
    
    private static readonly IntPtr LoggingCallbackPointer = Marshal.GetFunctionPointerForDelegate(LoggingCallbackDelegate);
    
    #endregion
    
    private static class API
    {
        const string LibraryName = "qpdf";
        
        /* GENERAL */

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int questpdf_get_compatibility_version();
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int questpdf_check_compatibility_by_calculating_sum(int x, int y);
    
        /* JOBS */
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr qpdfjob_init();
    
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void qpdfjob_cleanup(ref IntPtr jobHandle);
    
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int qpdfjob_initialize_from_json(IntPtr jobHandle, [MarshalAs(UnmanagedType.LPUTF8Str)] string json);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr qpdfjob_create_qpdf(IntPtr jobHandle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int qpdfjob_write_qpdf(IntPtr jobHandle, IntPtr documentHandle);

        /* CUSTOM */
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void qpdf_cleanup(ref IntPtr documentHandle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int questpdf_job_get_xmp_metadata(IntPtr jobHandle, IntPtr documentHandle, out IntPtr buffer, out UIntPtr length);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int questpdf_job_set_xmp_metadata(IntPtr jobHandle, IntPtr documentHandle, byte[] content, UIntPtr length);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void qpdf_oh_free_buffer(ref IntPtr buffer);

        /* IN-MEMORY BUFFERS */

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int qpdfjob_register_buffer_input(IntPtr jobHandle, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr data, UIntPtr length);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int qpdfjob_register_buffer_output(IntPtr jobHandle, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr callback, IntPtr udata);

        /* LOGGING */
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(qpdflogger_create))]
        public static extern IntPtr qpdflogger_create();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(qpdflogger_cleanup))]
        public static extern void qpdflogger_cleanup(ref IntPtr loggerHandle);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(qpdflogger_set_error))]
        public static extern void qpdflogger_set_error(IntPtr loggerHandle, int destination, IntPtr callBackHandler, IntPtr udata);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(qpdfjob_set_logger))]
        public static extern void qpdfjob_set_logger(IntPtr jobHandle, IntPtr loggerHandle);
    }
}
