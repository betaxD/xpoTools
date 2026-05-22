using System.Diagnostics;
using System.Text;

namespace XpoFast.Cli;

/// <summary>
/// Thread-safe progress bar that renders in-place on the console.
/// Uses ANSI colors when the terminal supports them.
/// </summary>
public sealed class ConsoleProgress : IDisposable
{
    private const int BarWidth = 36;

    private readonly string _label;
    private readonly int _total;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly bool _ansi;
    private readonly object _lock = new();

    private int _current;
    private bool _finished;

    public ConsoleProgress(string label, int total)
    {
        _label = label;
        _total = total;
        _ansi  = IsAnsiSupported();

        Console.OutputEncoding = Encoding.UTF8;
        Render(0, null);
    }

    public void Tick(string? currentItem = null)
    {
        var n = Interlocked.Increment(ref _current);
        lock (_lock) Render(n, currentItem);
    }

    public void Finish(string? summary = null)
    {
        lock (_lock)
        {
            if (_finished) return;
            _finished = true;
            Render(_total > 0 ? _total : _current, summary, done: true);
            Console.WriteLine();
        }
    }

    public void Dispose() => Finish();

    // ─────────────────────────────────────────────────────────────────────────

    private void Render(int current, string? label, bool done = false)
    {
        var pct    = _total > 0 ? Math.Min(1.0, (double)current / _total) : 1.0;
        var filled = (int)(pct * BarWidth);
        var empty  = BarWidth - filled;

        var elapsed = _sw.Elapsed.TotalSeconds;
        var rate    = elapsed > 0 && current > 0 ? current / elapsed : 0.0;

        var bar = new string('█', filled) + new string('░', empty);

        // Trim the item label to fit
        var itemStr = label ?? "";
        if (itemStr.Length > 32)
            itemStr = "…" + itemStr[^31..];

        string line;
        if (_ansi)
        {
            var barColor  = done ? "\x1b[32m" : "\x1b[36m";  // green when done, cyan in progress
            var reset     = "\x1b[0m";
            var pctStr    = $"{pct * 100,5:F1}%";
            line = $"\r  \x1b[1m{_label,-8}{reset}  {barColor}[{bar}]{reset}  {pctStr}  {current}/{_total}  {rate,6:F0}/s  {itemStr,-32}\x1b[K";
        }
        else
        {
            line = $"\r  {_label,-8}  [{bar}]  {pct * 100,5:F1}%  {current}/{_total}  {rate,6:F0}/s  {itemStr,-32}";
        }

        Console.Write(line);
    }

    private static bool IsAnsiSupported()
    {
        if (Console.IsOutputRedirected) return false;
        // Windows 10 1607+ and all modern terminals support ANSI via VT processing
        if (OperatingSystem.IsWindows())
        {
            var handle = GetStdHandle(-11); // STD_OUTPUT_HANDLE
            return GetConsoleMode(handle, out var mode) && (mode & 0x0004) != 0; // ENABLE_VIRTUAL_TERMINAL_PROCESSING
        }
        return true;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);
}

/// <summary>Simple spinner for indeterminate progress (e.g., file reads).</summary>
public sealed class ConsoleSpinner : IDisposable
{
    private readonly string _label;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private int _count;

    public ConsoleSpinner(string label)
    {
        _label = label;
        Console.Write($"  {_label}  ");
    }

    public void Tick(string? info = null)
    {
        _count++;
        var frames = new[] { '⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏' };
        var spin = frames[_count % frames.Length];
        var inf  = info ?? "";
        if (inf.Length > 50) inf = "…" + inf[^49..];
        Console.Write($"\r  {_label}  {spin}  {_count} elements  {inf,-52}");
    }

    public void Finish(string summary)
    {
        var elapsed = _sw.Elapsed;
        Console.Write($"\r  {_label}  ✓  {summary}  ({elapsed.TotalSeconds:F2}s){new string(' ', 20)}");
        Console.WriteLine();
    }

    public void Dispose() { }
}
