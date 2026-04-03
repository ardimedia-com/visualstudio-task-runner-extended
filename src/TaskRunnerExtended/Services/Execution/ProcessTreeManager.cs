namespace TaskRunnerExtended.Services.Execution;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Manages a process and its entire process tree via Windows Job Objects.
/// Ensures that when a task is stopped, all child processes are terminated too.
/// Also handles graceful shutdown (Ctrl+C) before force kill.
/// </summary>
public sealed partial class ProcessTreeManager : IDisposable
{
    private nint _jobHandle;
    private Process? _process;
    private bool _disposed;

    /// <summary>
    /// Starts a process and assigns it to a Job Object for tree management.
    /// </summary>
    public Process Start(ProcessStartInfo startInfo)
    {
        // Create a Job Object that kills all processes when closed
        _jobHandle = CreateJobObject(nint.Zero, null);
        if (_jobHandle == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create Job Object.");
        }

        // Configure: kill all processes in the job when the job handle is closed
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
            },
        };

        int infoSize = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        if (!SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation, ref info, infoSize))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to configure Job Object.");
        }

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start process.");

        // Assign process to job object
        if (!AssignProcessToJobObject(_jobHandle, _process.Handle))
        {
            // Non-fatal: process runs but without tree management
            Debug.WriteLine($"Warning: Failed to assign process {_process.Id} to Job Object.");
        }

        return _process;
    }

    /// <summary>
    /// Gracefully stops the process tree: sends Ctrl+C, waits for graceful shutdown,
    /// then force kills if the process doesn't exit in time.
    /// </summary>
    /// <param name="gracefulTimeoutMs">Milliseconds to wait after Ctrl+C before force killing.</param>
    public async Task StopAsync(int gracefulTimeoutMs = 5000)
    {
        if (_process is null || _process.HasExited) return;

        try
        {
            // Try graceful shutdown via Ctrl+C (SIGINT equivalent on Windows)
            if (GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
            {
                // Wait for graceful exit
                using var cts = new CancellationTokenSource(gracefulTimeoutMs);
                try
                {
                    await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                    return; // Process exited gracefully
                }
                catch (OperationCanceledException)
                {
                    // Timeout — fall through to force kill
                }
            }
        }
        catch
        {
            // Ctrl+C failed — fall through to force kill
        }

        // Force kill via Job Object (terminates entire tree)
        ForceKill();
    }

    /// <summary>
    /// Immediately kills the entire process tree without graceful shutdown.
    /// </summary>
    public void ForceKill()
    {
        try
        {
            if (_jobHandle != nint.Zero)
            {
                TerminateJobObject(_jobHandle, 1);
            }
            else if (_process is not null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort — process may already be gone
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_process is not null && !_process.HasExited)
            {
                ForceKill();
            }
        }
        catch
        {
            // Dispose must not throw
        }

        if (_jobHandle != nint.Zero)
        {
            CloseHandle(_jobHandle);
            _jobHandle = nint.Zero;
        }

        _process?.Dispose();
        _process = null;
    }

    // --- Win32 P/Invoke ---

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const uint CTRL_C_EVENT = 0;

    private enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateJobObject(nint lpJobAttributes, string? lpName);

    [LibraryImport("kernel32.dll", EntryPoint = "SetInformationJobObject", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(
        nint hJob, JobObjectInfoType infoType,
        ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, int cbJobObjectInfoLength);

    [LibraryImport("kernel32.dll", EntryPoint = "AssignProcessToJobObject", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(nint hJob, nint hProcess);

    [LibraryImport("kernel32.dll", EntryPoint = "TerminateJobObject", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateJobObject(nint hJob, uint uExitCode);

    [LibraryImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint hObject);

    [LibraryImport("kernel32.dll", EntryPoint = "GenerateConsoleCtrlEvent", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
}
