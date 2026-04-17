using System.Runtime.InteropServices;
using System.Text;

namespace HideAppKernel;

public static class Injector
{
    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll")]
    static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll")]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

    public static void Inject(int processId, string dllPath) {
        var hProcess = OpenProcess(0x1F0FFF, false, processId);
        if (hProcess == IntPtr.Zero) return;

        // Используем Unicode версию (W)
        var loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
    
        // Выделяем память под Unicode строку (длина * 2 байта)
        uint size = (uint)((dllPath.Length + 1) * 2);
        var allocMemAddress = VirtualAllocEx(hProcess, IntPtr.Zero, size, 0x3000, 0x40);
    
        // Записываем путь в Unicode
        byte[] pathBytes = Encoding.Unicode.GetBytes(dllPath);
        WriteProcessMemory(hProcess, allocMemAddress, pathBytes, (uint)pathBytes.Length, out _);
    
        CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, IntPtr.Zero);
    }
    
    [DllImport("psapi.dll", SetLastError = true)]
    static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true)]
    static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, uint nSize);

    public static void Eject(int processId, string dllName) {
        var hProcess = OpenProcess(0x1F0FFF, false, processId);
        var hMods = new IntPtr[1024];
        var cb = (uint)(IntPtr.Size * hMods.Length);

        if (!EnumProcessModules(hProcess, hMods, cb, out var cbNeeded)) return;
        for (var i = 0; i < (cbNeeded / IntPtr.Size); i++) {
            var sb = new StringBuilder(1024);
            GetModuleBaseName(hProcess, hMods[i], sb, (uint)sb.Capacity);

            if (sb.ToString() != dllName) continue;
            var freeLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "FreeLibrary");
            CreateRemoteThread(hProcess, IntPtr.Zero, 0, freeLibraryAddr, hMods[i], 0, IntPtr.Zero);
            Console.WriteLine("DLL успешно выгружена, окно снова видно.");
            return;
        }
    }

    public static bool GetInjectStatus(int processId, string dllName)
    {
        var hProcess = OpenProcess(0x1F0FFF, false, processId);
        var hMods = new IntPtr[1024];
        var cb = (uint)(IntPtr.Size * hMods.Length);

        if (!EnumProcessModules(hProcess, hMods, cb, out var cbNeeded)) return false;
        for (var i = 0; i < (cbNeeded / IntPtr.Size); i++) {
            var sb = new StringBuilder(1024);
            GetModuleBaseName(hProcess, hMods[i], sb, (uint)sb.Capacity);

            if (sb.ToString() == dllName)return true;
        }

        return false;
    }
}