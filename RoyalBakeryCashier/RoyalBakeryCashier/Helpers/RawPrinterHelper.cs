using System.Runtime.InteropServices;
using System.Text;

namespace RoyalBakeryCashier.Helpers;

/// <summary>
/// Sends raw ESC/POS bytes directly to a Windows printer via the Print Spooler API.
/// Works with USB, network, and shared printers — no need for file-share paths.
/// </summary>
public static class RawPrinterHelper
{
#if WINDOWS
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] DOCINFOA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    // EnumPrinters to list installed printers without System.Drawing.Common
    private const int PRINTER_ENUM_LOCAL = 0x00000002;
    private const int PRINTER_ENUM_CONNECTIONS = 0x00000004;

    [DllImport("winspool.drv", EntryPoint = "EnumPrintersA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool EnumPrinters(int flags, string? name, int level,
        IntPtr pPrinterEnum, int cbBuf, out int pcbNeeded, out int pcReturned);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct PRINTER_INFO_2
    {
        public IntPtr pServerName;
        public IntPtr pPrinterName;
        public IntPtr pShareName;
        public IntPtr pPortName;
        public IntPtr pDriverName;
        public IntPtr pComment;
        public IntPtr pLocation;
        public IntPtr pDevMode;
        public IntPtr pSepFile;
        public IntPtr pPrintProcessor;
        public IntPtr pDatatype;
        public IntPtr pParameters;
        public IntPtr pSecurityDescriptor;
        public int Attributes;
        public int Priority;
        public int DefaultPriority;
        public int StartTime;
        public int UntilTime;
        public int Status;
        public int cJobs;
        public int AveragePPM;
    }
#endif

    /// <summary>
    /// Send raw bytes to the named printer.
    /// </summary>
    public static bool SendBytesToPrinter(string printerName, byte[] data)
    {
#if WINDOWS
        IntPtr hPrinter = IntPtr.Zero;
        bool ok = false;

        try
        {
            if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                return false;

            var di = new DOCINFOA { pDocName = "Receipt", pDataType = "RAW" };
            if (!StartDocPrinter(hPrinter, 1, di))
                return false;

            if (!StartPagePrinter(hPrinter))
                return false;

            IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
            try
            {
                Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);
                ok = WritePrinter(hPrinter, pUnmanagedBytes, data.Length, out _);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pUnmanagedBytes);
            }

            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);
        }
        finally
        {
            if (hPrinter != IntPtr.Zero)
                ClosePrinter(hPrinter);
        }
        return ok;
#else
        return false;
#endif
    }

    /// <summary>
    /// Get all installed printer names via EnumPrinters P/Invoke.
    /// </summary>
    public static List<string> GetInstalledPrinters()
    {
        var list = new List<string>();
#if WINDOWS
        try
        {
            int flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;
            EnumPrinters(flags, null, 2, IntPtr.Zero, 0, out int needed, out _);

            if (needed <= 0) return list;

            IntPtr pAddr = Marshal.AllocHGlobal(needed);
            try
            {
                if (EnumPrinters(flags, null, 2, pAddr, needed, out _, out int returned))
                {
                    int structSize = Marshal.SizeOf<PRINTER_INFO_2>();
                    for (int i = 0; i < returned; i++)
                    {
                        var info = Marshal.PtrToStructure<PRINTER_INFO_2>(pAddr + i * structSize);
                        string? name = Marshal.PtrToStringAnsi(info.pPrinterName);
                        if (!string.IsNullOrEmpty(name))
                            list.Add(name);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pAddr);
            }
        }
        catch { }
#endif
        return list;
    }

    /// <summary>
    /// Find the first installed printer whose name contains any of the search terms.
    /// </summary>
    public static string? FindThermalPrinter()
    {
        var searchTerms = new[] { "EPSON", "TM-T82", "TM-T20", "Receipt", "Thermal", "POS-" };
        var printers = GetInstalledPrinters();
        foreach (var printer in printers)
        {
            foreach (var term in searchTerms)
            {
                if (printer.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return printer;
            }
        }
        return null;
    }
}
