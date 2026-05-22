using System.Diagnostics;
using System.Text;
using XpoFast.Cli;
using XpoFast.Core;

Console.OutputEncoding = Encoding.UTF8;

var args = Environment.GetCommandLineArgs()[1..];
if (args.Length == 0) { PrintHelp(); return 0; }

return args[0].ToLowerInvariant() switch
{
    "split" => RunSplit(args[1..]),
    "merge" => RunMerge(args[1..]),
    "info"  => RunInfo(args[1..]),
    "--help" or "-h" or "help" => PrintHelp(),
    _ => PrintHelp()
};

// ═════════════════════════════════════════════════════════════════════════════
// SPLIT
// ═════════════════════════════════════════════════════════════════════════════
static int RunSplit(string[] args)
{
    if (args.Length == 0) { Console.Error.WriteLine("Usage: xpo split <file.xpo> [options]"); return 1; }

    var file          = args[0];
    var outputDir     = ArgValue(args, "-o", "--output") ?? Path.GetFileNameWithoutExtension(file);
    var styleStr      = ArgValue(args, "-s", "--style") ?? "Default";
    var style         = PathStyleResolver.Parse(styleStr);
    var xpp           = HasFlag(args, "--xpp");
    var noClobber     = HasFlag(args, "--no-clobber");
    var overwriteNewer= HasFlag(args, "--overwrite-newer");
    var threads       = int.TryParse(ArgValue(args, "--threads"), out var t) ? t : Environment.ProcessorCount;
    var includes      = ArgValues(args, "--include");
    var excludes      = ArgValues(args, "--exclude");

    if (!File.Exists(file)) { Console.Error.WriteLine($"[error] File not found: {file}"); return 1; }

    var fi = new FileInfo(file);
    Console.WriteLine();
    Console.WriteLine($"  \x1b[1mxpo split\x1b[0m  {fi.Name}  ({FormatBytes(fi.Length)})");
    Console.WriteLine();

    // ── Step 1: Parse ────────────────────────────────────────────────────────
    var items = new List<XpoItem>();
    var spinner = new ConsoleSpinner("Parsing ");
    var sw = Stopwatch.StartNew();

    foreach (var item in XpoParser.ParseFile(file, count => spinner.Tick(item: null)))
    {
        items.Add(item);
        spinner.Tick(items[^1].ToString());
    }

    spinner.Finish($"{items.Count} elements found");

    if (items.Count == 0) { Console.Error.WriteLine("[warn] No elements parsed."); return 0; }

    // Populate project nodes if needed for project-based styles
    if (style is PathStyleKind.Project or PathStyleKind.FlatProject or PathStyleKind.All or PathStyleKind.Mazzy)
        XpoParser.PopulateProjectNodes(items);

    // ── Step 2: Split ────────────────────────────────────────────────────────
    Console.WriteLine();

    var opts = new SplitOptions
    {
        OutputDir             = outputDir,
        Style                 = style,
        Xpp                   = xpp,
        NoClobber             = noClobber,
        OverwriteNewer        = overwriteNewer,
        Include               = includes,
        Exclude               = excludes,
        MaxDegreeOfParallelism= threads,
    };

    using var progress = new ConsoleProgress("Writing ", items.Count);

    var result = XpoSplitter.Split(items, opts,
        onProgress: (current, total, path) => progress.Tick(Path.GetFileName(path)));

    progress.Finish($"{result.Written} written  {result.Skipped} skipped  {result.Errors} errors");

    Console.WriteLine();
    PrintSummary("Split", result.Written, result.Skipped, result.Errors,
        sw.Elapsed, outputDir);
    Console.WriteLine();
    return result.Errors > 0 ? 1 : 0;
}

// ═════════════════════════════════════════════════════════════════════════════
// MERGE
// ═════════════════════════════════════════════════════════════════════════════
static int RunMerge(string[] args)
{
    if (args.Length == 0) { Console.Error.WriteLine("Usage: xpo merge <source-dir-or-xpo...> -o <output.xpo>"); return 1; }

    var output    = ArgValue(args, "-o", "--output") ?? "merged.xpo";
    var pattern   = ArgValue(args, "-p", "--pattern") ?? "*.xpo";
    var recursive = HasFlag(args, "-r", "--recursive");
    var noClobber = HasFlag(args, "--no-clobber");

    // Remaining args (not flags) are source paths
    var sources = args
        .Where(a => !a.StartsWith('-') && a != args[0] || a == args[0])
        .Take(args.Length)
        .Where(a => !a.StartsWith('-'))
        .ToArray();

    if (sources.Length == 0) { Console.Error.WriteLine("[error] No source specified."); return 1; }

    Console.WriteLine();
    Console.WriteLine($"  \x1b[1mxpo merge\x1b[0m  → {output}");
    Console.WriteLine();

    var allItems = new List<XpoItem>();
    var opts     = new MergeOptions { OutputPath = output, NoClobber = noClobber, Pattern = pattern, Recursive = recursive };

    foreach (var src in sources)
    {
        if (Directory.Exists(src))
        {
            var searchOpt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(src, pattern, searchOpt);

            using var progress = new ConsoleProgress($"Reading", files.Length);
            int idx = 0;
            foreach (var f in files)
            {
                foreach (var item in XpoParser.ParseFile(f))
                    allItems.Add(item);
                progress.Tick(Path.GetFileName(f));
                idx++;
            }
            progress.Finish($"{allItems.Count} elements");
        }
        else if (File.Exists(src))
        {
            var spinner = new ConsoleSpinner($"Reading {Path.GetFileName(src)} ");
            foreach (var item in XpoParser.ParseFile(src, _ => spinner.Tick()))
                allItems.Add(item);
            spinner.Finish($"{allItems.Count} elements");
        }
        else
        {
            Console.Error.WriteLine($"[warn] Not found: {src}");
        }
    }

    if (allItems.Count == 0) { Console.Error.WriteLine("[warn] Nothing to merge."); return 0; }

    Console.WriteLine();
    using var mergeProgress = new ConsoleProgress("Merging ", allItems.Count);
    var sw = Stopwatch.StartNew();
    XpoMerger.Merge(allItems, opts, (cur, tot) => mergeProgress.Tick());
    mergeProgress.Finish($"→ {output}");

    Console.WriteLine();
    Console.WriteLine($"  \x1b[32m✓\x1b[0m  {allItems.Count} elements merged into {output}  ({sw.Elapsed.TotalSeconds:F2}s)");
    Console.WriteLine();
    return 0;
}

// ═════════════════════════════════════════════════════════════════════════════
// INFO
// ═════════════════════════════════════════════════════════════════════════════
static int RunInfo(string[] args)
{
    if (args.Length == 0) { Console.Error.WriteLine("Usage: xpo info <file.xpo>"); return 1; }

    var file = args[0];
    if (!File.Exists(file)) { Console.Error.WriteLine($"[error] File not found: {file}"); return 1; }

    var fi = new FileInfo(file);
    Console.WriteLine();
    Console.WriteLine($"  \x1b[1m{fi.Name}\x1b[0m  {FormatBytes(fi.Length)}  {fi.LastWriteTime:yyyy-MM-dd HH:mm}");
    Console.WriteLine();

    var items   = new List<XpoItem>();
    var spinner = new ConsoleSpinner("Parsing ");
    foreach (var item in XpoParser.ParseFile(file, _ => spinner.Tick()))
        items.Add(item);
    spinner.Finish($"{items.Count} elements");

    Console.WriteLine();

    // Group by AOT category
    var byCategory = items
        .GroupBy(i => i.Type.AotPath[0])
        .OrderByDescending(g => g.Count());

    int maxLen = byCategory.Max(g => g.Key.Length);

    Console.WriteLine($"  {"Category",-{maxLen}}  Count");
    Console.WriteLine($"  {new string('─', maxLen)}  ─────");
    foreach (var g in byCategory)
        Console.WriteLine($"  {g.Key,-{maxLen}}  {g.Count(),5}");

    Console.WriteLine($"  {new string('─', maxLen)}  ─────");
    Console.WriteLine($"  {"Total",-{maxLen}}  {items.Count,5}");
    Console.WriteLine();
    return 0;
}

// ═════════════════════════════════════════════════════════════════════════════
// Helpers
// ═════════════════════════════════════════════════════════════════════════════
static int PrintHelp()
{
    Console.WriteLine("""

  xpo — fast XPO file tool (Dynamics AX)

  Commands:
    xpo split <file.xpo> [options]         Split XPO into individual files
    xpo merge <dir|file...> [options]      Merge XPO files into one
    xpo info  <file.xpo>                   Show element statistics

  Split options:
    -o, --output <dir>         Output directory  (default: file name without ext)
    -s, --style  <style>       Path style: Default|Flat|AOT|FlatAOT|Project|FlatProject|All|mazzy
    --xpp                      Convert classes/jobs to .xpp format
    --no-clobber               Skip existing files
    --overwrite-newer          Overwrite only if source is newer
    --threads <n>              Parallel threads  (default: CPU count)
    --include <pattern>        Include only matching paths (wildcard)
    --exclude <pattern>        Exclude matching paths (wildcard)

  Merge options:
    -o, --output <file>        Output XPO file  (default: merged.xpo)
    -p, --pattern <pat>        File glob pattern  (default: *.xpo)
    -r, --recursive            Search directories recursively
    --no-clobber               Skip if output exists

  Examples:
    xpo split Project.xpo -o src --style Flat
    xpo split Project.xpo -o src --xpp --threads 8
    xpo merge src\ -o merged.xpo -r
    xpo info  Project.xpo

""");
    return 0;
}

static void PrintSummary(string cmd, int written, int skipped, int errors, TimeSpan elapsed, string dest)
{
    var rate = elapsed.TotalSeconds > 0 ? written / elapsed.TotalSeconds : 0;
    var color = errors > 0 ? "\x1b[33m" : "\x1b[32m";
    Console.WriteLine(
        $"  {color}✓\x1b[0m  {written} files → {dest}" +
        $"  │  {skipped} skipped  {errors} errors" +
        $"  │  {elapsed.TotalSeconds:F2}s  ({rate:F0} files/s)");
}

static bool HasFlag(string[] args, params string[] names) =>
    args.Any(a => names.Contains(a, StringComparer.OrdinalIgnoreCase));

static string? ArgValue(string[] args, params string[] names)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (names.Contains(args[i], StringComparer.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

static string[] ArgValues(string[] args, string name)
{
    var result = new List<string>();
    for (int i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            result.Add(args[i + 1]);
    return result.ToArray();
}

static string FormatBytes(long bytes) => bytes switch
{
    < 1024        => $"{bytes} B",
    < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
    _             => $"{bytes / (1024.0 * 1024):F1} MB"
};
