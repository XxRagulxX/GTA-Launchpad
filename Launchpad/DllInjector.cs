using System.Runtime.InteropServices;
using System.Text;

namespace Launchpad;

internal static class DllInjector
{
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
            int err = Marshal.GetLastWin32Error();
            log(err == 5
                ? $"Access denied opening game process (PID {pid}). Run Launchpad as Administrator."
                : $"Failed to open game process (PID {pid}). Win32 error {err}.");
            return 0;
        }

        int injected = 0;
        try
        {
            // Resolve LoadLibraryW in the TARGET process.
            // Under Wine/Linux each process may load kernel32 at a different address,
            // so we look it up via Toolhelp rather than using our own process's base.
            var ourKernel32    = NativeMethods.GetModuleHandle("kernel32.dll");
            var ourLLW         = NativeMethods.GetProcAddress(ourKernel32, "LoadLibraryW");
            long llwOffset     = ourLLW.ToInt64() - ourKernel32.ToInt64();

            var remoteKernel32 = NativeMethods.GetRemoteModuleBase((uint)pid, "kernel32.dll");
            var loadLibraryW   = remoteKernel32 != IntPtr.Zero
                ? new IntPtr(remoteKernel32.ToInt64() + llwOffset)
                : ourLLW; // fallback

            if (loadLibraryW == IntPtr.Zero)
            {
                log("Failed to resolve LoadLibraryW in target process.");
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
                try { File.Copy(dll, copy, overwrite: true); }
                catch (Exception ex)
                {
                    log($"Couldn't stage {Path.GetFileName(dll)}: {ex.Message}");
                    continue;
                }

                var pathBytes  = Encoding.Unicode.GetBytes(copy + "\0");
                var remoteAddr = NativeMethods.VirtualAllocEx(handle, IntPtr.Zero, (IntPtr)pathBytes.Length,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);

                if (remoteAddr == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    log($"VirtualAllocEx failed for {Path.GetFileName(dll)} (error {err}).");
                    continue;
                }

                if (!NativeMethods.WriteProcessMemory(handle, remoteAddr, pathBytes, (uint)pathBytes.Length, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    log($"WriteProcessMemory failed for {Path.GetFileName(dll)} (error {err}).");
                    continue;
                }

                var thread = StartRemoteThread(handle, loadLibraryW, remoteAddr);
                if (thread == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    log($"Could not start remote thread for {Path.GetFileName(dll)} (error {err}).");
                    continue;
                }

                NativeMethods.WaitForSingleObject(thread, 10_000);
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

    // Tries NtCreateThreadEx (hidden from debuggers) first, falls back to CreateRemoteThread.
    // The hidden flag bypasses user-mode anti-cheat monitors that watch for new remote threads.
    private static IntPtr StartRemoteThread(IntPtr hProcess, IntPtr startAddr, IntPtr param)
    {
        int status = NativeMethods.NtCreateThreadEx(
            out IntPtr t,
            NativeMethods.THREAD_ALL_ACCESS,
            IntPtr.Zero, hProcess, startAddr, param,
            NativeMethods.THREAD_CREATE_FLAGS_HIDE,
            UIntPtr.Zero, UIntPtr.Zero, UIntPtr.Zero,
            IntPtr.Zero);
        if (status == 0 && t != IntPtr.Zero) return t;

        status = NativeMethods.NtCreateThreadEx(
            out t,
            NativeMethods.THREAD_ALL_ACCESS,
            IntPtr.Zero, hProcess, startAddr, param,
            0,
            UIntPtr.Zero, UIntPtr.Zero, UIntPtr.Zero,
            IntPtr.Zero);
        if (status == 0 && t != IntPtr.Zero) return t;

        return NativeMethods.CreateRemoteThread(hProcess, IntPtr.Zero, 0, startAddr, param, 0, IntPtr.Zero);
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
