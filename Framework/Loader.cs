using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;

// https://www.pinvoke.net/default.aspx/kernel32.openprocess
// https://www.pinvoke.net/default.aspx/kernel32.virtualallocex

namespace LibraryLoader.Framework
{
    public enum ProcessFlags : UInt32
    {
        All = 0x001F0FFF,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VirtualMemoryOperation = 0x00000008,
        VirtualMemoryRead = 0x00000010,
        VirtualMemoryWrite = 0x00000020,
        DuplicateHandle = 0x00000040,
        CreateProcess = 0x000000080,
        SetQuota = 0x00000100,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        QueryLimitedInformation = 0x00001000,
        Synchronize = 0x00100000
    }

    public enum ThreadFlags : UInt32
    {
        TERMINATE = 0x0001,
        SUSPEND_RESUME = 0x0002,
        GET_CONTEXT = 0x0008,
        SET_CONTEXT = 0x0010,
        SET_INFORMATION = 0x0020,
        QUERY_INFORMATION = 0x0040,
        SET_THREAD_TOKEN = 0x0080,
        IMPERSONATE = 0x0100,
        DIRECT_IMPERSONATION = 0x0200
    }

    public enum AllocationType : UInt32
    {
        Commit = 0x1000,
        Reserve = 0x2000,
        Decommit = 0x4000,
        Release = 0x8000,
        Reset = 0x80000,
        Physical = 0x400000,
        TopDown = 0x100000,
        WriteWatch = 0x200000,
        LargePages = 0x20000000
    }

    public enum MemoryProtection : UInt32
    {
        Execute = 0x10,
        ExecuteRead = 0x20,
        ExecuteReadWrite = 0x40,
        ExecuteWriteCopy = 0x80,
        NoAccess = 0x01,
        ReadOnly = 0x02,
        ReadWrite = 0x04,
        WriteCopy = 0x08,
        GuardModifierflag = 0x100,
        NoCacheModifierflag = 0x200,
        WriteCombineModifierflag = 0x400
    }

    public enum InjectionResults : byte
    {
        None,
        LibraryNotFound,
        ProcessNotFound,
        AlreadyInjected,
        HandleNotFound,
        KernalNotFound,
        AllocateFail,
        WriteFail,
        ThreadFail,
        Exception,
        Success
    }

    public static class FLoader
    {
        private static List<IntPtr> m_handleCache = new List<IntPtr>(); // Handle cache for processes we've already loaded into.

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, UInt32 size, Int32 lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttribute, IntPtr dwStackSize, IntPtr lpStartAddress,
        IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, UInt32 flAllocationType, UInt32 flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, Int32 bInheritHandle, UInt32 dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Int32 CloseHandle(IntPtr hObject);

        public static void ClearHandleCache()
        {
            m_handleCache.Clear();
        }

        public static bool IsModuleLoaded(Process process, string keyword, bool bForceCheck)
        {
            if (FProcess.IsValidProcess(process))
            {
                if (bForceCheck)
                {
                    List<ProcessModule> moudles = FProcess.GetModules(process);

                    foreach (ProcessModule module in moudles)
                    {
                        if (module.FileName.Contains(keyword))
                        {
                            if (!m_handleCache.Contains(process.Handle))
                            {
                                m_handleCache.Add(process.Handle);
                            }

                            return true;
                        }
                    }
                }
                else
                {
                    return m_handleCache.Contains(process.Handle);
                }
            }

            return false;
        }

        // Attempts to load a library into an individual process, adds to the "m_handleCache" list but does NOT remove old handles.
        public static InjectionResults LoadLibrary(Process process, string libraryFile)
        {
            if (File.Exists(libraryFile))
            {
                if (FProcess.IsValidProcess(process))
                {
                    if (!IsModuleLoaded(process, Path.GetFileNameWithoutExtension(libraryFile), false))
                    {
                        InjectionResults result = LoadLibraryInternal(process, libraryFile);

                        if (result == InjectionResults.Success)
                        {
                            m_handleCache.Add(process.Handle);
                        }

                        return result;
                    }
                    else
                    {
                        return InjectionResults.AlreadyInjected;
                    }
                }
                else
                {
                    return InjectionResults.ProcessNotFound;
                }
            }

            return InjectionResults.LibraryNotFound;
        }

        private static InjectionResults LoadLibraryInternal(Process process, string libraryFile)
        {
            if (!File.Exists(libraryFile))
            {
                return InjectionResults.LibraryNotFound;
            }

            try
            {
                IntPtr processHandle = OpenProcess(Convert.ToUInt32(ProcessFlags.All), 1, Convert.ToUInt32(process.Id));

                if (processHandle == IntPtr.Zero)
                {
                    return InjectionResults.HandleNotFound;
                }

                IntPtr loadLibraryAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

                if (loadLibraryAddress == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    return InjectionResults.KernalNotFound;
                }

                IntPtr allocatedAddress = VirtualAllocEx(processHandle, IntPtr.Zero, new IntPtr(libraryFile.Length), (Convert.ToUInt32(AllocationType.Commit) | Convert.ToUInt32(AllocationType.Reserve)), Convert.ToUInt32(MemoryProtection.ExecuteReadWrite));

                if (allocatedAddress == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    return InjectionResults.AllocateFail;
                }

                byte[] bytes = Encoding.ASCII.GetBytes(libraryFile);
                int bWroteMemory = WriteProcessMemory(processHandle, allocatedAddress, bytes, Convert.ToUInt32(bytes.Length), 0);

                if (bWroteMemory == 0)
                {
                    CloseHandle(processHandle);
                    return InjectionResults.WriteFail;
                }

                IntPtr threadHandle = CreateRemoteThread(processHandle, IntPtr.Zero, IntPtr.Zero, loadLibraryAddress, allocatedAddress, 0, IntPtr.Zero);

                if (threadHandle == IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                    return InjectionResults.ThreadFail;
                }

                CloseHandle(threadHandle);
                CloseHandle(processHandle);

                return InjectionResults.Success;
            }
            catch
            {
                return InjectionResults.Exception;
            }
        }
    }
}
