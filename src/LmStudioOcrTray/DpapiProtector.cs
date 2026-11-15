using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace LmStudioOcrTray;

internal static class DpapiProtector
{
    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    public static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var input = CreateBlob(bytes);
        try
        {
            if (!CryptProtectData(
                    ref input,
                    "LM Studio OCR Tray API Key",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN,
                    out var output))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                var protectedBytes = CopyBlob(output);
                return Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                {
                    LocalFree(output.pbData);
                }
            }
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(input.pbData);
            }
        }
    }

    public static string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64))
        {
            return string.Empty;
        }

        var bytes = Convert.FromBase64String(protectedBase64);
        var input = CreateBlob(bytes);
        try
        {
            if (!CryptUnprotectData(
                    ref input,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN,
                    out var output))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                return Encoding.UTF8.GetString(CopyBlob(output));
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                {
                    LocalFree(output.pbData);
                }
            }
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(input.pbData);
            }
        }
    }

    private static DataBlob CreateBlob(byte[] data)
    {
        var pointer = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, pointer, data.Length);
        return new DataBlob { cbData = data.Length, pbData = pointer };
    }

    private static byte[] CopyBlob(DataBlob blob)
    {
        var data = new byte[blob.cbData];
        if (blob.cbData > 0)
        {
            Marshal.Copy(blob.pbData, data, 0, blob.cbData);
        }
        return data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
