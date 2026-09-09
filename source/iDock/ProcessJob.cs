using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace iDock;

// A job owns only processes started by this app and their descendants. Closing it
// also covers UxPlay's self-restart and beacon without killing unrelated receivers.
internal sealed class ProcessJob : IDisposable
{
    private readonly SafeFileHandle handle;
    public Process Process { get; }
    public ProcessJob(ProcessStartInfo info)
    {
        handle = CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid) throw new Win32Exception();
        var limits = new ExtendedLimits();
        limits.Basic.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if (!SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>()))
        { handle.Dispose(); throw new Win32Exception(); }
        if (info.RedirectStandardOutput || info.RedirectStandardError)
            throw new ArgumentException("Owned background engines must not redirect console streams.");
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Flags = 1,
            ShowWindow = (short)(info.WindowStyle == ProcessWindowStyle.Normal ? 1 : 0) };
        var command = new StringBuilder(string.Join(" ", new[] { info.FileName }.Concat(info.ArgumentList).Select(Quote)));
        // Native CreateProcess must honor the child's environment overrides too.
        // In particular, the screenshot endpoint belongs only to this control job;
        // changing the launcher process environment would leak it to future jobs.
        var environment = Marshal.StringToHGlobalUni(BuildEnvironmentBlock(info));
        ProcessInfo native;
        try
        {
            if (!CreateProcess(info.FileName, command, IntPtr.Zero, IntPtr.Zero, false,
                    0x08000404, environment, info.WorkingDirectory, ref startup, out native)) // suspended + no console + Unicode environment
            { var error = new Win32Exception(); handle.Dispose(); throw error; }
        }
        finally { Marshal.FreeHGlobal(environment); }
        try
        {
            if (!AssignProcessToJobObject(handle, native.Process)) throw new Win32Exception();
            Process = Process.GetProcessById((int)native.ProcessId);
            if (ResumeThread(native.Thread) == uint.MaxValue) throw new Win32Exception();
        }
        catch { TerminateProcess(native.Process, 1); handle.Dispose(); throw; }
        finally { CloseHandle(native.Thread); CloseHandle(native.Process); }
    }
    private static string Quote(string argument) => "\"" + Regex.Replace(
        Regex.Replace(argument, @"(\\*)""", "$1$1\\\""), @"(\\+)$", "$1$1") + "\"";
    internal static string BuildEnvironmentBlock(ProcessStartInfo info) => string.Join('\0',
        info.Environment.Where(pair => pair.Value is not null)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
    public bool IsRunning => !handle.IsClosed &&
        QueryInformationJobObject(handle, 1, out var info, (uint)Marshal.SizeOf<Accounting>(), IntPtr.Zero)
        && info.ActiveProcesses > 0;
    // Lifecycle monitoring must distinguish an API read failure from an exited job.
    internal bool ReadIsRunning()
    {
        if (handle.IsClosed) return false;
        if (!QueryInformationJobObject(handle, 1, out var info, (uint)Marshal.SizeOf<Accounting>(), IntPtr.Zero))
            throw new Win32Exception();
        return info.ActiveProcesses > 0;
    }
    internal bool ContainsProcess(uint processId)
    {
        if (handle.IsClosed || processId == 0) return false;
        using var process = OpenProcess(0x1000, false, processId); // PROCESS_QUERY_LIMITED_INFORMATION
        if (process.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 87) return false; // The enumerated process has already exited.
            throw new Win32Exception(error);
        }
        // Test the opened process object against this exact job, not a saved PID/name.
        // A recycled PID or a same-name receiver in another session cannot match.
        if (!IsProcessInJob(process, handle, out var belongs)) throw new Win32Exception();
        return belongs;
    }
    public void Dispose() { handle.Dispose(); Process.Dispose(); }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
        public short ShowWindow, ReservedBytes;
        public IntPtr ReservedPointer, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo
    { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }

    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint LimitFlags;
        public UIntPtr MinWorkingSet, MaxWorkingSet;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters
    { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Accounting
    {
        public long TotalUserTime, TotalKernelTime, PeriodUserTime, PeriodKernelTime;
        public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int type, ref ExtendedLimits info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(SafeProcessHandle process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool belongs);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int type, out Accounting info, uint length, IntPtr returnedLength);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string directory,
        ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")] private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
