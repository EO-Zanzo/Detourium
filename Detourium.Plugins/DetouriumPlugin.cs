using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Detourium
{
    /// <summary>
    /// A plugin to be loaded in the specified process.
    /// </summary>
    public abstract class DetouriumPlugin
    {
        /// <summary>
        /// The unique identifying name of the plugin.
        /// <para> The name specified must be alphanumeric with no spaces, and up to 80 characters in length. </para>
        /// </summary>
        public abstract string PluginName { get; }

        /// <summary>
        /// The version of the plugin.
        /// </summary>
        public abstract double PluginVersion { get; }

        /// <summary>
        /// Configuration for the plugin.
        /// </summary>
        public virtual PluginConfiguration Configuration { get; set; }
            = new PluginConfiguration();
        
        /// <summary>
        /// Installs the plugin into the matching process.
        /// </summary>
        /// <param name="processId"> The ID of the process of which to inject. </param>
        public void Install(int processId) =>
            this.Install(Array.Find(Process.GetProcesses(), p => p.Id == processId));

        /// <summary>
        /// Installs the plugin into the matching process.
        /// </summary>
        /// <param name="processName"> The name of the process of which to inject. </param>
        public void Install(string processName) =>
            this.Install(Array.Find(Process.GetProcesses(), p => p.ProcessName == processName));

        /// <summary>
        /// Installs the plugin into the process started.
        /// </summary>
        /// <param name="startInfo"> The parameters to start the process with. </param>
        /// <param name="useExistingProcess">
        /// If the process is already running, use the existing process instead of instantiating a new one.
        /// </param>
        public void Install(ProcessStartInfo startInfo, bool useExistingProcess)
        {
            if (!useExistingProcess) {
                this.Install(Process.Start(startInfo).Id);
                return;
            }

            var existingProcess = Process.GetProcesses().ToList().Find(p => p.GetProcessName() == startInfo.FileName);
            this.Install(existingProcess != null ? existingProcess.Id : Process.Start(startInfo).Id);
        }

        private void Install(Process process)
        {
            if (string.IsNullOrWhiteSpace(this.PluginName))
                throw new DetouriumPluginException($"The plugin '{ this.PluginName } v{ this.PluginVersion }' was unable to be loaded.", ErrorCode.MissingProperty);

            if (!this.PluginName.All(char.IsLetterOrDigit) || this.PluginName.Length > 80)
                throw new DetouriumPluginException($"The plugin '{ this.PluginName } v{ this.PluginVersion }' was unable to be loaded.", ErrorCode.InvalidPluginName);

            if (process == null)
                throw new DetouriumPluginException($"The plugin '{ this.PluginName } v{ this.PluginVersion }' was unable to be loaded.", ErrorCode.ProcessNotFound);

            var processPlatform = RuntimeInstaller.ProcessHelpers.GetPlatform(process.Id);
            var processModules = RuntimeInstaller.ModuleCollector.CollectModules(process);

            var temporaryPluginGuid = Guid.NewGuid().ToString();

            // if the CLR isn't already loaded, inject the CLR and Detourium runtime
            if (!processModules.Any(module => module.ModuleName == "initclr.dll")) {
                Process.Start(RuntimeInstaller.GetInjectStartInfo(process.Id, temporaryPluginGuid));

                // wait until the CLR is loaded and then (hopefully) the Detourium runtime is running
                // TODO: make sure the runtime is actually running instead of waiting... (ideally list appdomain dlls or similar)
                while (!RuntimeInstaller.ModuleCollector.CollectModules(process).Any(module => module.ModuleName == "initclr.dll")) {
                    Thread.Sleep(500);
                }
            }

            var installLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Detourium");
            var pluginFileName = $"{process.Id}-{this.PluginName}-{temporaryPluginGuid}.tmp";

            File.Copy(Assembly.GetEntryAssembly().Location, Path.Combine(installLocation, "plugins", pluginFileName), overwrite: true);
        }

        /// <summary>
        /// Whenever a plugin is being uninstalled.
        /// <para>
        /// (note: remember to uninstall any detours you have installed.)
        /// </para>
        /// </summary>
        public abstract bool OnUninstall();

        /// <summary>
        /// Whenever a plugin is being installed.
        /// </summary>
        public abstract void OnInstalled();
    }

    /// <summary>
    /// Optional additional configuration for Detourium plugins.
    /// </summary>
    public class PluginConfiguration
    {
        /// <summary>
        /// Ensure only higher versions of the plugin will be loaded.
        /// </summary>
        public bool EnforceVersionPriority;

        /// <summary>
        /// Allocate a console attached to the process displaying debugging information.
        /// </summary>
        public bool DisplayConsole = true;
    }

    /// <summary>
    /// A class for assisting with installing and managing detours.
    /// </summary>
    public class Detour
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleA", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtect(IntPtr lpAddress, int dwSize, int flNewProtect, ref int lpflOldProtect);

        [DllImport("kernel32.dll", EntryPoint = "lstrcpynA", CharSet = CharSet.Ansi)]
        private static extern IntPtr lstrcpyn(byte[] lpString1, byte[] lpString2, int iMaxLength);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        private const int PAGE_EXECUTE_READWRITE = 0x40;
        private IntPtr ProcAddress;
        private int lpflOldProtect = 0;

        private byte[] oldEntry = new byte[5];
        private byte[] newEntry = new byte[5];
        private IntPtr OldAddress;

        /// <summary>
        /// Indicates whether the detour has been successfully installed.
        /// </summary>
        public bool SuccessfullyInstalled = false;

        /// <summary>
        /// Install the specified detour in the running process.
        /// </summary>
        /// <param name="moduleName"> The name of the module to hook. </param>
        /// <param name="procName"> The name of the procedure to hook. </param>
        /// <param name="callback"> The callback for the hook. </param>
        /// <returns></returns>
        public Detour Install(string moduleName, string procName, Delegate callback)
        {
            var lpAddress = Marshal.GetFunctionPointerForDelegate(callback);

            var hModule = GetModuleHandle(moduleName);
            if (hModule == IntPtr.Zero)
                throw new DetouriumPluginException("Unable to install detour. The module name specified does not exist.", ErrorCode.InvalidModuleName);

            this.ProcAddress = GetProcAddress(hModule, procName);
            if (ProcAddress == IntPtr.Zero)
                throw new DetouriumPluginException("Unable to install detour. The procedure name specified does not exist.", ErrorCode.InvalidModuleName);

            if (!VirtualProtect(ProcAddress, 5, PAGE_EXECUTE_READWRITE, ref lpflOldProtect))
                throw new DetouriumPluginException("Unable to install detour. The virtual protection was unable to be modified.", ErrorCode.InvalidModuleName);

            Marshal.Copy(ProcAddress, oldEntry, 0, 5);
            newEntry = AddBytes(new byte[1] { 233 }, BitConverter.GetBytes((int)lpAddress - (int)ProcAddress - 5));
            Marshal.Copy(newEntry, 0, ProcAddress, 5);
            oldEntry = AddBytes(oldEntry, new byte[5] { 233, 0, 0, 0, 0 });
            OldAddress = lstrcpyn(oldEntry, oldEntry, 0);
            Marshal.Copy(BitConverter.GetBytes((double)((int)ProcAddress - (int)OldAddress - 5)), 0, (IntPtr)(OldAddress.ToInt32() + 6), 4);
            FreeLibrary(hModule);

            this.SuccessfullyInstalled = true;
            return this;
        }

        /// <summary>
        /// Suspend the detour by restoring the JMP instruction to the original address.
        /// </summary>
        public void Suspend() => Marshal.Copy(oldEntry, 0, ProcAddress, 5);

        /// <summary>
        /// Continue the detour by changing the JMP instruction to the new address.
        /// </summary>
        public void Continue() => Marshal.Copy(newEntry, 0, ProcAddress, 5);

        /// <summary>
        /// Uninstall the detour by restoring the the JMP instruction to the original address.
        /// </summary>
        /// <returns></returns>
        public bool Uninstall()
        {
            if (ProcAddress == IntPtr.Zero)
                return false;

            Marshal.Copy(oldEntry, 0, ProcAddress, 5);
            ProcAddress = IntPtr.Zero;
            return true;
        }

        private byte[] AddBytes(byte[] a, byte[] b)
        {
            var arrayList = new ArrayList();

            for (var i = 0; i < a.Length; i++) arrayList.Add(a[i]);
            for (var i = 0; i < b.Length; i++) arrayList.Add(b[i]);

            return (byte[])arrayList.ToArray(typeof(byte));
        }
    }
}

internal static class RuntimeInstaller
{
    internal static ProcessStartInfo GetInjectStartInfo(int processId, string optionalArgs = "")
    {
        // get Detourium injectclr location
        var installLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Detourium");

        // get x86 or x64 inject path
        var injectFilename = Path.Combine(installLocation, ProcessHelpers.GetPlatform(processId), "inject.exe");
        var spyFilename = Path.Combine(installLocation, "Detourium.dll");

        // build args
        var args = string.Format("-m {0} -i \"{1}\" -l {2} -a \"{3}\" -n {4}", "EntryPoint", spyFilename, "Detourium.Runtime.DetouriumRuntime", optionalArgs, processId);

        // create and return the process info
        return new ProcessStartInfo {
            CreateNoWindow = true,
            UseShellExecute = false,
            FileName = injectFilename,
            Arguments = args
        };
    }

    internal static class ModuleCollector
    {
        public static List<Module> CollectModules(Process process)
        {
            var collectedModules = new List<Module>();

            var modulePointers = new IntPtr[0];

            // determine number of modules
            if (!Native.EnumProcessModulesEx(process.Handle, modulePointers, 0, out var bytesNeeded, (uint)Native.ModuleFilter.ListModulesAll)) {
                return collectedModules;
            }

            var totalNumberofModules = bytesNeeded / IntPtr.Size;
            modulePointers = new IntPtr[totalNumberofModules];

            // collect modules from the process
            if (Native.EnumProcessModulesEx(process.Handle, modulePointers, bytesNeeded, out bytesNeeded, (uint)Native.ModuleFilter.ListModulesAll)) {
                for (var index = 0; index < totalNumberofModules; index++) {
                    var moduleFilePath = new StringBuilder(1024);
                    Native.GetModuleFileNameEx(process.Handle, modulePointers[index], moduleFilePath, (uint)(moduleFilePath.Capacity));

                    var moduleName = Path.GetFileName(moduleFilePath.ToString());
                    var moduleInformation = new Native.ModuleInformation();
                    Native.GetModuleInformation(process.Handle, modulePointers[index], out moduleInformation, (uint)(IntPtr.Size * (modulePointers.Length)));

                    // convert to a normalized module and add it to our list
                    var module = new Module(moduleName, moduleInformation.lpBaseOfDll, moduleInformation.SizeOfImage);
                    collectedModules.Add(module);
                }
            }

            return collectedModules;
        }

        public static class Native
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct ModuleInformation
            {
                public IntPtr lpBaseOfDll;
                public uint SizeOfImage;
                public IntPtr EntryPoint;
            }

            internal enum ModuleFilter
            {
                ListModulesDefault = 0x0,
                ListModules32Bit = 0x01,
                ListModules64Bit = 0x02,
                ListModulesAll = 0x03,
            }

            [DllImport("psapi.dll")]
            public static extern bool EnumProcessModulesEx(IntPtr hProcess, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U4)] [In][Out] IntPtr[] lphModule, int cb, [MarshalAs(UnmanagedType.U4)] out int lpcbNeeded, uint dwFilterFlag);

            [DllImport("psapi.dll")]
            public static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In] [MarshalAs(UnmanagedType.U4)] uint nSize);

            [DllImport("psapi.dll", SetLastError = true)]
            public static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out ModuleInformation lpmodinfo, uint cb);
        }

        public class Module
        {
            public Module(string moduleName, IntPtr baseAddress, uint size)
            {
                this.ModuleName = moduleName;
                this.BaseAddress = baseAddress;
                this.Size = size;
            }

            public string ModuleName { get; set; }
            public IntPtr BaseAddress { get; set; }
            public uint Size { get; set; }
        }
    }

    internal static class ProcessHelpers
    {
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

        /// <summary>
        /// Check whether the process specified is 32-bit or 64-bit.
        /// </summary>
        /// <returns> If true, the process is 32-bit, otherwise 64-bit. </returns>
        public static bool IsWow64Process(int id)
        {
            if (!Environment.Is64BitOperatingSystem)
                return true;

            IntPtr processHandle;

            try { processHandle = Process.GetProcessById(id).Handle; }
            catch { return false; } // access is denied to the process

            return IsWow64Process(processHandle, out var retVal) && retVal;
        }

        /// <summary>
        /// Check the platform of the process specified.
        /// </summary>
        /// <param name="id"> Process Id </param>
        /// <returns> x86 or x64 </returns>
        public static string GetPlatform(int id) => IsWow64Process(id) ? "x86" : "x64";
    }
}

internal static class Extensions
{
    [DllImport("kernel32.dll")]
    private static extern uint QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, [In, Out] ref uint lpdwSize);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("psapi.dll")]
    static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In] [MarshalAs(UnmanagedType.U4)] int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CloseHandle(IntPtr hObject);

    internal static string GetProcessName(this Process process)
    {
        try
        {
            var processHandle = OpenProcess(0x0400 | 0x0010, false, process.Id);

            if (processHandle == IntPtr.Zero)
                return null;

            var sb = new StringBuilder(4000);
            var result = (string)null;

            if (GetModuleFileNameEx(processHandle, IntPtr.Zero, sb, 4000) > 0)
                result = sb.ToString();

            CloseHandle(processHandle);
            return result;
        }
        catch { return null; }
    }

    internal static string GetMainModuleFileName(this Process process, int buffer = 1024)
    {
        var fileNameBuilder = new StringBuilder(buffer);
        var bufferLength = (uint)fileNameBuilder.Capacity + 1;

        try
        {
            return QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength) != 0 ? fileNameBuilder.ToString() : null;
        }
        catch { return "-access-denied-"; }
    }
}