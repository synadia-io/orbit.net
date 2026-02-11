// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// Borrowed from https://stackoverflow.com/questions/3342941/kill-child-process-when-parent-process-is-killed/37034966#37034966
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Synadia.Orbit.Testing.NatsServerProcessManager;

/// <summary>
/// Allows processes to be automatically killed if this parent process unexpectedly quits.
/// This feature requires Windows 8 or greater. On Windows 7, nothing is done.
/// </summary>
/// <remarks>References:
///  https://stackoverflow.com/a/4657392/386091
///  https://stackoverflow.com/a/9164742/386091.</remarks>
#pragma warning disable SA1204
#pragma warning disable SA1129
#pragma warning disable SA1201
#pragma warning disable SA1117
#pragma warning disable SA1400
#pragma warning disable SA1311
#pragma warning disable SA1308
#pragma warning disable SA1413
#pragma warning disable SA1121
#pragma warning disable SA1402
public static class ChildProcessTracker
{
    /// <summary>
    /// Add the process to be tracked. If our current process is killed, the child processes
    /// that we are tracking will be automatically killed, too. If the child process terminates
    /// first, that's fine, too.
    /// </summary>
    /// <param name="process">Process.</param>
    public static void AddProcess(Process process)
    {
        if (s_jobHandle != IntPtr.Zero)
        {
            var success = AssignProcessToJobObject(s_jobHandle, process.Handle);
            if (!success && !process.HasExited)
            {
                throw new Win32Exception();
            }
        }
    }

    static ChildProcessTracker()
    {
        // This feature requires Windows 8 or later. To support Windows 7, requires
        //  registry settings to be added if you are using Visual Studio plus an
        //  app.manifest change.
        //  https://stackoverflow.com/a/4232259/386091
        //  https://stackoverflow.com/a/9507862/386091
        if (Environment.OSVersion.Version < new Version(6, 2))
        {
            return;
        }

        // The job name is optional (and can be null), but it helps with diagnostics.
        //  If it's not null, it has to be unique. Use SysInternals' Handle command-line
        //  utility: handle -a ChildProcessTracker
        var jobName = "ChildProcessTracker" + Process.GetCurrentProcess().Id;
        s_jobHandle = CreateJobObject(IntPtr.Zero, jobName);

        var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION();

        // This is the key flag. When our process is killed, Windows will automatically
        //  close the job handle, and when that happens, we want the child processes to
        //  be killed, too.
        info.LimitFlags = JOBOBJECTLIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        extendedInfo.BasicLimitInformation = info;

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var extendedInfoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

            if (!SetInformationJobObject(s_jobHandle, JobObjectInfoType.ExtendedLimitInformation,
                    extendedInfoPtr, (uint)length))
            {
                throw new Win32Exception();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(extendedInfoPtr);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string name);

    [DllImport("kernel32.dll")]
    static extern bool SetInformationJobObject(IntPtr job, JobObjectInfoType infoType,
        IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    // Windows will automatically close any open job handles when our process terminates.
    //  This can be verified by using SysInternals' Handle utility. When the job handle
    //  is closed, the child processes will be killed.
    private static readonly IntPtr s_jobHandle;
}

#pragma warning disable SA1602

/// <summary>
/// Job object information types for Windows API.
/// </summary>
public enum JobObjectInfoType
{
    /// <summary>Associate completion port information.</summary>
    AssociateCompletionPortInformation = 7,

    /// <summary>Basic limit information.</summary>
    BasicLimitInformation = 2,

    /// <summary>Basic UI restrictions.</summary>
    BasicUIRestrictions = 4,

    /// <summary>End of job time information.</summary>
    EndOfJobTimeInformation = 6,

    /// <summary>Extended limit information.</summary>
    ExtendedLimitInformation = 9,

    /// <summary>Security limit information.</summary>
    SecurityLimitInformation = 5,

    /// <summary>Group information.</summary>
    GroupInformation = 11,
}

/// <summary>
/// Basic limit information for a Windows job object.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
{
    /// <summary>Per-process user time limit.</summary>
    public long PerProcessUserTimeLimit;

    /// <summary>Per-job user time limit.</summary>
    public long PerJobUserTimeLimit;

    /// <summary>Limit flags.</summary>
    public JOBOBJECTLIMIT LimitFlags;

    /// <summary>Minimum working set size.</summary>
    public UIntPtr MinimumWorkingSetSize;

    /// <summary>Maximum working set size.</summary>
    public UIntPtr MaximumWorkingSetSize;

    /// <summary>Active process limit.</summary>
    public uint ActiveProcessLimit;

    /// <summary>Affinity mask.</summary>
    public long Affinity;

    /// <summary>Priority class.</summary>
    public uint PriorityClass;

    /// <summary>Scheduling class.</summary>
    public uint SchedulingClass;
}

/// <summary>
/// Job object limit flags.
/// </summary>
[Flags]
public enum JOBOBJECTLIMIT : uint
{
    /// <summary>Kill all processes when the job handle is closed.</summary>
    JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000,
}

/// <summary>
/// I/O counters for a Windows job object.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct IO_COUNTERS
{
    /// <summary>Read operation count.</summary>
    public ulong ReadOperationCount;

    /// <summary>Write operation count.</summary>
    public ulong WriteOperationCount;

    /// <summary>Other operation count.</summary>
    public ulong OtherOperationCount;

    /// <summary>Read transfer count.</summary>
    public ulong ReadTransferCount;

    /// <summary>Write transfer count.</summary>
    public ulong WriteTransferCount;

    /// <summary>Other transfer count.</summary>
    public ulong OtherTransferCount;
}

/// <summary>
/// Extended limit information for a Windows job object.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
{
    /// <summary>Basic limit information.</summary>
    public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;

    /// <summary>I/O counters.</summary>
    public IO_COUNTERS IoInfo;

    /// <summary>Process memory limit.</summary>
    public UIntPtr ProcessMemoryLimit;

    /// <summary>Job memory limit.</summary>
    public UIntPtr JobMemoryLimit;

    /// <summary>Peak process memory used.</summary>
    public UIntPtr PeakProcessMemoryUsed;

    /// <summary>Peak job memory used.</summary>
    public UIntPtr PeakJobMemoryUsed;
}
