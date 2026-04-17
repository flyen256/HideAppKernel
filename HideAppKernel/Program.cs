using System.Diagnostics;

namespace HideAppKernel;

internal static class Program
{
    private static bool _running = true;
    private static List<Process> _processes = [];
    private static readonly List<int> InjectedProcesses = [];
    
    private static readonly CancellationTokenSource CancellationTokenSource = new();
    private static readonly Thread FetchProcessesThread = new(() => FetchProcesses(CancellationTokenSource.Token));
    
    private const string DllName = "HideAppKernelLib.dll";
    private static string DllPath => Environment.CurrentDirectory + $"\\{DllName}";
    
    private static void Main()
    {
        if (!File.Exists(DllPath)) {
            Console.WriteLine("DLL не найдена!");
            return;
        }
    
        FetchProcessesThread.Start();
        string currentInput = "";

        while (_running)
        {
            // Рисуем меню в самом верху
            Console.SetCursorPosition(0, 0);
            DrawMenu();
        
            // Очищаем область ввода перед выводом новой (чтобы не было хвостов)
            Console.WriteLine("\n" + new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, (_processes.Count + 1 > 0 ? _processes.Count + 1 : 0));
            Console.Write($"> Введите номер (q - выход): {currentInput}_   ");

            // Неблокирующее чтение
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) {
                    HandleAction(currentInput);
                    currentInput = "";
                    Console.Clear(); // Полная очистка только после действия
                }
                else if (key.Key == ConsoleKey.Backspace && currentInput.Length > 0) {
                    currentInput = currentInput[..^1];
                }
                else if (key.Key == ConsoleKey.Q) {
                    _running = false;
                    CancellationTokenSource.Cancel();
                }
                else if (char.IsDigit(key.KeyChar)) {
                    currentInput += key.KeyChar;
                }
            }
            Thread.Sleep(50); // Частота обновления ~20 кадров в сек
        }
    }
    
    private static void HandleAction(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        if (int.TryParse(input, out var index) && index >= 0 && index < _processes.Count)
        {
            var selected = _processes[index];

            try 
            {
                if (!InjectedProcesses.Contains(selected.Id))
                {
                    Injector.Inject(selected.Id, DllPath);
                    InjectedProcesses.Add(selected.Id);
                }
                else
                {
                    Injector.Eject(selected.Id, DllName);
                    InjectedProcesses.Remove(selected.Id);
                }
            }
            catch (Exception ex)
            {
                Console.SetCursorPosition(0, _processes.Count + 5);
                Console.WriteLine($"Ошибка: {ex.Message}");
                Thread.Sleep(2000);
            }
        }
    }

    private static void FetchProcesses(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            _processes = GetCurrentProcesses().ToList();
            Thread.Sleep(1000);
        }
    }

    private static Process[] GetCurrentProcesses()
    {
        return Process.GetProcesses()
            .Where(p => p.MainWindowHandle != IntPtr.Zero)
            .OrderBy(p => p.Id)
            .Select(p =>
            {
                if(Injector.GetInjectStatus(p.Id, DllName) && !InjectedProcesses.Contains(p.Id))
                    InjectedProcesses.Add(p.Id);
                return p;
            })
            .ToArray();
    }

    private static void DrawMenu()
    {
        for (var i = 0; i < _processes.Count; i++)
        {
            var process = _processes[i];
            var injected = InjectedProcesses.Contains(process.Id);
            Console.WriteLine($"({i}) ({(injected ? "X" : "")}) {process.ProcessName}");
        }
    }
}