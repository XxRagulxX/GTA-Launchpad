using System.Runtime.InteropServices;
using System.Text;

namespace Launchpad;

internal static class DllInjector
{
    public static int InjectAll(int pid, IReadOnlyList<string> dllPaths, string tempDir, Action<string> log)
    {
        if (dllPaths.Count == 0) return 0;

        CrashLog.Write($"  InjectAll: entered. pid={pid} dlls={dllPaths.Count} tempDir={tempDir}");
        Directory.CreateDirectory(tempDir);
        CrashLog.Write($"  InjectAll: temp dir ready");
        foreach (var stale in new DirectoryInfo(tempDir).GetFiles())
        {
            try { stale.Delete(); } catch { }
        }

        CrashLog.Write($"  InjectAll: calling OpenProcess on pid={pid}");
        var handle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_CREATE_THREAD |
            NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_WRITE |
            NativeMethods.PROCESS_VM_READ,
            false, (uint)pid);
        CrashLog.Write($"  InjectAll: OpenProcess returned handle=0x{handle:X}");

        if (handle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            log(err == 5
                ? $"Access denied opening game process (PID {pid}). Run Launchpad as Administrator."
                : $"Failed to open game process (PID {pid}). Win32 error {err}.");
            return 0;
        }

        int injected = 0;
        try
        {
            CrashLog.Write("  InjectAll: GetModuleHandle kernel32.dll");
            var kernel32     = NativeMethods.GetModuleHandle("kernel32.dll");
            CrashLog.Write($"  InjectAll: kernel32=0x{kernel32:X}");
            var loadLibraryW = NativeMethods.GetProcAddress(kernel32, "LoadLibraryW");
            CrashLog.Write($"  InjectAll: LoadLibraryW=0x{loadLibraryW:X}");
            if (loadLibraryW == IntPtr.Zero)
            {
                log("Failed to resolve LoadLibraryW in kernel32.dll.");
                return 0;
            }

            foreach (var dll in dllPaths)
            {
                if (!File.Exists(dll))
                {
                    log($"Skipped: file not found: {dll}");
                    continue;
                }

                string copy = Path.Combine(tempDir, $"LP_{RandomSuffix(5)}.dll");
                CrashLog.Write($"  InjectAll: staging {Path.GetFileName(dll)} → {copy}");
                try { File.Copy(dll, copy, overwrite: true); }
                catch (Exception ex)
                {
                    log($"Couldn't stage {Path.GetFileName(dll)}: {ex.Message}");
                    continue;
                }
                CrashLog.Write("  InjectAll: staged OK");

                var pathBytes  = Encoding.Unicode.GetBytes(copy + "\0");
                CrashLog.Write("  InjectAll: VirtualAllocEx");
                var remoteAddr = NativeMethods.VirtualAllocEx(handle, IntPtr.Zero, (IntPtr)pathBytes.Length,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
                CrashLog.Write($"  InjectAll: VirtualAllocEx returned 0x{remoteAddr:X}");

                if (remoteAddr == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    log($"VirtualAllocEx failed for {Path.GetFileName(dll)} (error {err}).");
                    continue;
                }

                CrashLog.Write("  InjectAll: WriteProcessMemory");
                if (!NativeMethods.WriteProcessMemory(handle, remoteAddr, pathBytes, (uint)pathBytes.Length, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    log($"WriteProcessMemory failed for {Path.GetFileName(dll)} (error {err}).");
                    continue;
                }
                CrashLog.Write("  InjectAll: WriteProcessMemory OK");

                CrashLog.Write("  InjectAll: CreateRemoteThread");
                var thread = NativeMethods.CreateRemoteThread(
                    handle, IntPtr.Zero, 0, loadLibraryW, remoteAddr, 0, IntPtr.Zero);
                CrashLog.Write($"  InjectAll: CreateRemoteThread returned 0x{thread:X}");

                if (thread == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    log($"CreateRemoteThread failed for {Path.GetFileName(dll)} (error {err}).");
                    continue;
                }

                NativeMethods.CloseHandle(thread);
                log($"Injected: {Path.GetFileName(dll)}");
                injected++;
            }
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }

        return injected;
    }

    private static readonly Random Rng = new();
    private static string RandomSuffix(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var buf = new char[length];
        for (int i = 0; i < length; i++) buf[i] = chars[Rng.Next(chars.Length)];
        return new string(buf);
    }
}
