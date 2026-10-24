using System.Text;

namespace Launchpad;

internal static class DllInjector
{
    /// <summary>
    /// Injects each DLL into the target process via LoadLibraryW + CreateRemoteThread.
    /// Copies each DLL to a temp file first to avoid locking/AV issues on the source.
    /// Returns the count of successfully injected DLLs.
    /// </summary>
    public static int InjectAll(int pid, IReadOnlyList<string> dllPaths, string tempDir, Action<string> log)
    {
        if (dllPaths.Count == 0) return 0;

        Directory.CreateDirectory(tempDir);
        foreach (var stale in new DirectoryInfo(tempDir).GetFiles())
        {
            try { stale.Delete(); } catch { }
        }

        var handle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_CREATE_THREAD |
            NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_WRITE |
            NativeMethods.PROCESS_VM_READ,
            false, (uint)pid);

        if (handle == IntPtr.Zero)
        {
            log("Failed to open a handle to the game process.");
            return 0;
        }

        int injected = 0;
        try
        {
            var kernel32 = NativeMethods.GetModuleHandle("kernel32.dll");
            var loadLibraryW = NativeMethods.GetProcAddress(kernel32, "LoadLibraryW");
            if (loadLibraryW == IntPtr.Zero)
            {
                log("Failed to resolve LoadLibraryW.");
                return 0;
            }

            foreach (var dll in dllPaths)
            {
                if (!File.Exists(dll)) { log($"Skipped '{dll}': file does not exist."); continue; }

                string copy = Path.Combine(tempDir, $"LP_{RandomSuffix(5)}.dll");
                try { File.Copy(dll, copy, overwrite: true); }
                catch (Exception) { log($"Couldn't stage '{dll}' — file may be locked or antivirus blocked it."); continue; }

                var pathBytes = Encoding.Unicode.GetBytes(copy + "\0");

                var remoteAddr = NativeMethods.VirtualAllocEx(handle, IntPtr.Zero, (IntPtr)pathBytes.Length,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
                if (remoteAddr == IntPtr.Zero) { log($"Couldn't allocate memory for '{dll}'."); continue; }

                if (!NativeMethods.WriteProcessMemory(handle, remoteAddr, pathBytes, (uint)pathBytes.Length, out _))
                { log($"Couldn't write '{dll}' into the target process."); continue; }

                var thread = NativeMethods.CreateRemoteThread(
                    handle, IntPtr.Zero, 0, loadLibraryW, remoteAddr, 0, IntPtr.Zero);
                if (thread == IntPtr.Zero) { log($"Failed to start remote thread for '{dll}'."); continue; }

                NativeMethods.CloseHandle(thread);
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
