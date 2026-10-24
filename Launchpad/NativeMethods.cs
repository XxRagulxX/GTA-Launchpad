using System.Runtime.InteropServices;

namespace Launchpad;

internal static class NativeMethods
{
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_CREATE_THREAD     = 0x0002;
    public const uint PROCESS_VM_OPERATION      = 0x0008;
    public const uint PROCESS_VM_WRITE          = 0x0020;
    public const uint PROCESS_VM_READ           = 0x0010;

    public const uint MEM_COMMIT     = 0x1000;
    public const uint MEM_RESERVE    = 0x2000;
    public const uint PAGE_READWRITE = 0x04;

    public const uint TH32CS_SNAPMODULE   = 0x00000008;
    public const uint TH32CS_SNAPMODULE32 = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MODULEENTRY32
    {
        public uint  dwSize;
        public uint  th32ModuleID;
        public uint  th32ProcessID;
        public uint  GlblcntUsage;
        public uint  ProccntUsage;
        public IntPtr modBaseAddr;
        public uint  modBaseSize;
        public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExePath;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize,
        uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes,
        uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags,
        IntPtr lpThreadId);

    // Lower-level thread creation — hidden from user-mode debuggers/monitors
    // flags: 0x4 = THREAD_CREATE_FLAGS_HIDE_FROM_DEBUGGER
    [DllImport("ntdll.dll")]
    public static extern int NtCreateThreadEx(
        out IntPtr hThread,
        uint        desiredAccess,
        IntPtr      objectAttributes,
        IntPtr      processHandle,
        IntPtr      startAddress,
        IntPtr      parameter,
        uint        createFlags,
        UIntPtr     zeroBits,
        UIntPtr     stackSize,
        UIntPtr     maximumStackSize,
        IntPtr      attributeList);

    public const uint THREAD_ALL_ACCESS              = 0x1FFFFF;
    public const uint THREAD_CREATE_FLAGS_HIDE       = 0x4;
    public const uint TH32CS_SNAPTHREAD              = 0x00000004;

    [StructLayout(LayoutKind.Sequential)]
    public struct THREADENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ThreadID;
        public uint th32OwnerProcessID;
        public int  tpBasePri;
        public int  tpDeltaPri;
        public uint dwFlags;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    public const uint THREAD_SUSPEND_RESUME = 0x0002;
    public const uint THREAD_GET_CONTEXT    = 0x0008;
    public const uint THREAD_SET_CONTEXT    = 0x0010;
    public const uint THREAD_QUERY_INFO     = 0x0040;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SuspendThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Module32FirstW(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Module32NextW(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    /// <summary>
    /// Returns the base address of the named module (.exe or .dll) inside the target process,
    /// or IntPtr.Zero if not found. Name comparison is case-insensitive.
    /// </summary>
    public static IntPtr GetRemoteModuleBase(uint pid, string moduleName)
    {
        IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid);
        if (snap == new IntPtr(-1)) return IntPtr.Zero;
        try
        {
            var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
            if (!Module32FirstW(snap, ref me)) return IntPtr.Zero;
            do
            {
                if (string.Equals(me.szModule, moduleName, StringComparison.OrdinalIgnoreCase))
                    return me.modBaseAddr;
            }
            while (Module32NextW(snap, ref me));
            return IntPtr.Zero;
        }
        finally
        {
            CloseHandle(snap);
        }
    }
}
