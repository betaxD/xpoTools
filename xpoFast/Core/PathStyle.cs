namespace XpoFast.Core;

public enum PathStyleKind { Default, Flat, AOT, FlatAOT, Project, FlatProject, All, Mazzy }

public static class PathStyleResolver
{
    public static PathStyleKind Parse(string s) => s.ToLowerInvariant() switch
    {
        "flat"        => PathStyleKind.Flat,
        "aot"         => PathStyleKind.AOT,
        "flataot"     => PathStyleKind.FlatAOT,
        "project"     => PathStyleKind.Project,
        "flatproject" => PathStyleKind.FlatProject,
        "all"         => PathStyleKind.All,
        "mazzy"       => PathStyleKind.Mazzy,
        _             => PathStyleKind.Default,
    };

    /// <summary>
    /// Returns one or more output paths for the given item.
    /// Project-based styles return one path per group node; if none, returns empty.
    /// </summary>
    public static IEnumerable<string> Resolve(
        XpoItem item,
        string[] baseParts,
        string ext,
        PathStyleKind style)
    {
        return style switch
        {
            PathStyleKind.Flat        => ResolveFlat(item, baseParts, ext),
            PathStyleKind.AOT         => ResolveAot(item, baseParts, ext),
            PathStyleKind.FlatAOT     => ResolveFlatAot(item, baseParts, ext),
            PathStyleKind.Project     => ResolveProject(item, baseParts, ext),
            PathStyleKind.FlatProject => ResolveFlatProject(item, baseParts, ext),
            PathStyleKind.All         => ResolveAll(item, baseParts, ext),
            PathStyleKind.Mazzy       => ResolveMazzy(item, baseParts, ext),
            _                         => ResolveDefault(item, baseParts, ext),
        };
    }

    // ─── Simple (single-path) styles ─────────────────────────────────────────

    private static IEnumerable<string> ResolveDefault(XpoItem item, string[] base_, string ext)
    {
        yield return Join(base_, item.Type.AotPath, [FileName(item, ext)]);
    }

    private static IEnumerable<string> ResolveFlat(XpoItem item, string[] base_, string ext)
    {
        yield return Join(base_, [FileName(item, ext)]);
    }

    private static IEnumerable<string> ResolveAot(XpoItem item, string[] base_, string ext)
    {
        yield return Join(base_, item.Type.AotPath, [$"{item.Name}{ext}"]);
    }

    private static IEnumerable<string> ResolveFlatAot(XpoItem item, string[] base_, string ext)
    {
        yield return Join(base_, [item.Type.OneLevelAotPath, $"{item.Name}{ext}"]);
    }

    // ─── Project-based (multi-path) styles ───────────────────────────────────

    private static IEnumerable<string> ResolveProject(XpoItem item, string[] base_, string ext)
    {
        var fn = FileName(item, ext);
        return item.GroupNodes.Count == 0
            ? []
            : item.GroupNodes
                .Select(n => Join(base_,
                    [n.Project?.Name.Trim() ?? ""],
                    n.Path,
                    [fn]))
                .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ResolveFlatProject(XpoItem item, string[] base_, string ext)
    {
        var fn = FileName(item, ext);
        return item.GroupNodes.Count == 0
            ? []
            : item.GroupNodes
                .Select(n => Join(base_,
                    [n.Project?.Name.Trim() ?? ""],
                    [fn]))
                .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ResolveAll(XpoItem item, string[] base_, string ext)
    {
        var fn = FileName(item, ext);
        return item.GroupNodes.Count == 0
            ? []
            : item.GroupNodes
                .Select(n => Join(base_,
                    [n.Project?.Name.Trim() ?? ""],
                    n.Path,
                    item.Type.AotPath,
                    [fn]))
                .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ResolveMazzy(XpoItem item, string[] base_, string ext)
    {
        var fn = FileName(item, ext);

        string[] subDir = fn switch
        {
            _ when fn.StartsWith("Job_", StringComparison.OrdinalIgnoreCase) && ext == ".xpp" => ["Examples"],
            _ when fn.EndsWith("test.xpp", StringComparison.OrdinalIgnoreCase)                => ["Tests"],
            _                                                                                   => ["Src"],
        };

        if (item.GroupNodes.Count == 0)
        {
            yield return Join(base_, subDir, [fn]);
            yield break;
        }

        foreach (var path in item.GroupNodes
            .Select(n => Join(base_, subDir, n.Path, [fn]))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return path;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string FileName(XpoItem item, string ext) =>
        $"{item.Type.FilePrefix}_{item.Name}{ext}";

    private static string Join(params string[][] parts)
    {
        var segments = parts
            .SelectMany(p => p)
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return Path.Combine([.. segments]);
    }
}
