using System.Text;

namespace XpoFast.Core;

public sealed class MergeOptions
{
    public string OutputPath  { get; init; } = "merged.xpo";
    public bool NoClobber     { get; init; }
    public bool Force         { get; init; }
    public string Pattern     { get; init; } = "*.xpo";
    public bool Recursive     { get; init; }
    public Encoding Encoding  { get; init; } = new UTF8Encoding(false);
}

public sealed record MergeResult(int Merged, int Skipped, TimeSpan Elapsed);

public static class XpoMerger
{
    /// <summary>Merge XPO items already in memory into a single XPO file.</summary>
    public static MergeResult Merge(
        IReadOnlyList<XpoItem> items,
        MergeOptions opts,
        Action<int, int>? onProgress = null)
    {
        if (items.Count == 0) return new MergeResult(0, 0, TimeSpan.Zero);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (opts.NoClobber && File.Exists(opts.OutputPath))
            return new MergeResult(0, items.Count, sw.Elapsed);

        var dir = Path.GetDirectoryName(opts.OutputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Estimate capacity: average element is ~500 chars
        var sb = new StringBuilder(items[0].FileHeader.Length + items.Count * 512);
        sb.Append(items[0].FileHeader);

        for (int i = 0; i < items.Count; i++)
        {
            sb.Append("***Element: ");
            sb.Append(items[i].Text);
            onProgress?.Invoke(i + 1, items.Count);
        }

        sb.Append("***Element: END\r\n");
        File.WriteAllText(opts.OutputPath, sb.ToString(), opts.Encoding);
        return new MergeResult(items.Count, 0, sw.Elapsed);
    }

    /// <summary>Read individual XPO files from a directory and merge them.</summary>
    public static MergeResult MergeFromDirectory(
        string sourceDir,
        MergeOptions opts,
        Action<int, int, string>? onProgress = null)
    {
        var searchOpt = opts.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(sourceDir, opts.Pattern, searchOpt);

        if (files.Length == 0) return new MergeResult(0, 0, TimeSpan.Zero);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Parse all files to get items (preserving order)
        var allItems = new List<XpoItem>(files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            foreach (var item in XpoParser.ParseFile(files[i]))
                allItems.Add(item);
            onProgress?.Invoke(i + 1, files.Length, files[i]);
        }

        if (allItems.Count == 0) return new MergeResult(0, 0, sw.Elapsed);

        var result = Merge(allItems, opts);
        return result with { Elapsed = sw.Elapsed };
    }
}
