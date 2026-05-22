using System.Text;

namespace XpoFast.Core;

public sealed class SplitOptions
{
    public string OutputDir      { get; init; } = ".";
    public PathStyleKind Style   { get; init; } = PathStyleKind.Default;
    public bool Xpp              { get; init; }
    public bool NoClobber        { get; init; }
    public bool OverwriteNewer   { get; init; }
    public string[]? Include     { get; init; }
    public string[]? Exclude     { get; init; }
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
    public Encoding Encoding     { get; init; } = new UTF8Encoding(false);
}

public sealed record SplitResult(int Written, int Skipped, int Errors, TimeSpan Elapsed);

public static class XpoSplitter
{
    public static SplitResult Split(
        IReadOnlyList<XpoItem> items,
        SplitOptions opts,
        Action<int, int, string>? onProgress = null)
    {
        var baseParts = opts.OutputDir
            .Trim()
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                   StringSplitOptions.RemoveEmptyEntries);

        var written = 0;
        var skipped = 0;
        var errors  = 0;
        var sw      = System.Diagnostics.Stopwatch.StartNew();

        var jobs = BuildJobs(items, opts, baseParts).ToList();
        int total = jobs.Count;

        var lockObj = new object();
        int done = 0;

        var parallelOpts = new ParallelOptions
        {
            MaxDegreeOfParallelism = opts.MaxDegreeOfParallelism
        };

        Parallel.ForEach(jobs, parallelOpts, job =>
        {
            var outcome = WriteFile(job, opts);
            var current = Interlocked.Increment(ref done);

            lock (lockObj)
            {
                switch (outcome)
                {
                    case WriteOutcome.Written: written++; break;
                    case WriteOutcome.Skipped: skipped++; break;
                    case WriteOutcome.Error:   errors++;  break;
                }
                onProgress?.Invoke(current, total, job.DestPath);
            }
        });

        return new SplitResult(written, skipped, errors, sw.Elapsed);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private sealed record WriteJob(XpoItem Item, string Content, string DestPath, DateTime SourceTime);

    private static IEnumerable<WriteJob> BuildJobs(
        IReadOnlyList<XpoItem> items,
        SplitOptions opts,
        string[] baseParts)
    {
        foreach (var item in items)
        {
            string ext;
            string content;

            if (opts.Xpp && item.Type.Tag == "CLS")
            {
                ext     = ".xpp";
                content = XpoParser.BuildXppClass(item.Text);
                if (string.IsNullOrEmpty(content)) continue;
            }
            else if (opts.Xpp && item.Type.Tag == "JOB")
            {
                ext     = ".xpp";
                content = XpoParser.BuildXppJob(item.Text);
                if (string.IsNullOrEmpty(content)) continue;
            }
            else
            {
                ext     = ".xpo";
                content = BuildXpoContent(item);
            }

            var sourceTime = File.Exists(item.SourceFilePath)
                ? new FileInfo(item.SourceFilePath).LastWriteTime
                : DateTime.MinValue;

            foreach (var dest in PathStyleResolver.Resolve(item, baseParts, ext, opts.Style))
            {
                if (!PassesFilter(dest, opts.Include, opts.Exclude)) continue;
                yield return new WriteJob(item, content, dest, sourceTime);
            }
        }
    }

    private enum WriteOutcome { Written, Skipped, Error }

    private static WriteOutcome WriteFile(WriteJob job, SplitOptions opts)
    {
        try
        {
            if (File.Exists(job.DestPath))
            {
                if (opts.NoClobber) return WriteOutcome.Skipped;

                var destTime = new FileInfo(job.DestPath).LastWriteTime;
                if (!opts.OverwriteNewer && destTime >= job.SourceTime)
                    return WriteOutcome.Skipped;
            }

            var dir = Path.GetDirectoryName(job.DestPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir); // no-op if exists

            File.WriteAllText(job.DestPath, job.Content, opts.Encoding);
            return WriteOutcome.Written;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] {job.DestPath}: {ex.Message}");
            return WriteOutcome.Error;
        }
    }

    private static string BuildXpoContent(XpoItem item)
    {
        // Reconstruct: fileHeader + "***Element: " + text + "***Element: END\r\n"
        var sb = new StringBuilder(
            item.FileHeader.Length + item.Text.Length + 40);
        sb.Append(item.FileHeader);
        sb.Append("***Element: ");
        sb.Append(item.Text);
        sb.Append("***Element: END\r\n");
        return sb.ToString();
    }

    private static bool PassesFilter(string path, string[]? include, string[]? exclude)
    {
        if (include is { Length: > 0 })
        {
            var segs = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            if (!include.Any(pat => segs.Any(seg => MatchGlob(seg, pat)) || MatchGlob(path, pat)))
                return false;
        }

        if (exclude is { Length: > 0 })
        {
            var segs = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            if (exclude.Any(pat => segs.Any(seg => MatchGlob(seg, pat)) || MatchGlob(path, pat)))
                return false;
        }

        return true;
    }

    private static bool MatchGlob(string text, string pattern)
    {
        int t = 0, p = 0, star = -1, match = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || char.ToLowerInvariant(pattern[p]) == char.ToLowerInvariant(text[t])))
            { t++; p++; }
            else if (p < pattern.Length && pattern[p] == '*')
            { star = p++; match = t; }
            else if (star != -1)
            { p = star + 1; t = ++match; }
            else return false;
        }
        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }
}
