using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Dart;
// Windows closes all assigned children even if DART is terminated at logoff or crashes.
internal sealed class ChildProcessJob : IDisposable
{
    const uint KillOnJobClose = 0x2000;
    readonly SafeFileHandle job;
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits {
        public long PerProcessTime, PerJobTime;
        public uint Flags;
        public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcesses;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters {
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
    }
    [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern SafeFileHandle CreateJobObject(IntPtr attributes,string? name);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool SetInformationJobObject(SafeFileHandle job,int informationClass,ref ExtendedLimits limits,int size);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
    public ChildProcessJob() {
        job=CreateJobObject(IntPtr.Zero,null);
        if(job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var limits=new ExtendedLimits { Basic=new BasicLimits { Flags=KillOnJobClose } };
        if(!SetInformationJobObject(job,9,ref limits,Marshal.SizeOf<ExtendedLimits>())) {
            var error=Marshal.GetLastWin32Error(); job.Dispose(); throw new Win32Exception(error);
        }
    }
    public void Add(System.Diagnostics.Process process) {
        if(!AssignProcessToJobObject(job,process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Dispose() => job.Dispose();
}
