using System.Text;
using System.Text.RegularExpressions;

namespace XpoFast.Core;

public static partial class XpoParser
{
    private const string Delimiter = "***Element: ";

    [GeneratedRegex(@"^(?<Tag>\w+)[\s\S]+?\b(?<Type>\w+) #(?<Name>\w*)", RegexOptions.Multiline)]
    private static partial Regex ElementHeaderRegex();

    [GeneratedRegex(@"\b(?<SubTag>SHARED|PRIVATE)\b")]
    private static partial Regex PrnSubTagRegex();

    [GeneratedRegex(@"\bType:\s*(?<SubTag>\d+)")]
    private static partial Regex FtmSubTagRegex();

    [GeneratedRegex(@"GROUP #(?<Name>\w+)|ENDGROUP", RegexOptions.Multiline)]
    private static partial Regex GroupRegex();

    [GeneratedRegex(@"BEGINNODE(?<Body>[\s\S]*?)ENDNODE", RegexOptions.Multiline)]
    private static partial Regex NodeBlockRegex();

    [GeneratedRegex(@"NAME #(?<Name>\w+)")]
    private static partial Regex NodeNameRegex();

    [GeneratedRegex(@"UTILTYPE\s+(?<v>\d+)")]
    private static partial Regex UtilTypeRegex();

    [GeneratedRegex(@"NODETYPE\s+(?<v>\d+)")]
    private static partial Regex NodeTypeRegex();

    [GeneratedRegex(@"SOURCE #(?<Name>\w+)(?<Text>[\s\S]*?)ENDSOURCE")]
    private static partial Regex SourceRegex();

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stream-reads an XPO file and yields parsed XpoItems.
    /// Progress callback: (currentElement, estimatedTotal).
    /// </summary>
    public static IEnumerable<XpoItem> ParseFile(
        string filePath,
        Action<int>? onElement = null,
        Encoding? encoding = null)
    {
        encoding ??= DetectEncoding(filePath);

        // Reading the full file is O(n) and unavoidable; StreamReader line-by-line
        // would be slower for splitting by a multi-char delimiter.
        var content = File.ReadAllText(filePath, encoding);

        var parts = SplitByDelimiter(content);
        if (parts.Count < 3) yield break;

        // Last part must be the END sentinel
        var last = parts[^1].Trim();
        if (!last.Equals("END", StringComparison.OrdinalIgnoreCase)) yield break;

        var fileHeader = parts[0]; // everything before first ***Element:

        int count = 0;
        for (int i = 1; i < parts.Count - 1; i++)
        {
            var item = ParseElement(parts[i], fileHeader, filePath);
            if (item is null) continue;

            count++;
            onElement?.Invoke(count);
            yield return item;
        }
    }

    /// <summary>
    /// After parsing, call this to extract project group/node info for PRN elements.
    /// Needed only for Project/All/mazzy path styles.
    /// </summary>
    public static void PopulateProjectNodes(IReadOnlyList<XpoItem> items)
    {
        var projects = items.Where(i => i.Type.Tag == "PRN").ToList();
        if (projects.Count == 0) return;

        var allNodes = new List<XpoNode>();
        foreach (var proj in projects)
        {
            var nodes = ExtractGroupNodes(proj);
            proj.GroupNodes = nodes;
            allNodes.AddRange(nodes);
        }

        // Link non-project items to nodes that reference them by NodeType + UtilType + Name
        foreach (var item in items.Where(i => i.Type.Tag != "PRN"))
        {
            item.GroupNodes = allNodes
                .Where(n => n.NodeType == item.Type.NodeType
                         && n.UtilType == item.Type.UtilType
                         && string.Equals(n.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Source extraction (for --xpp mode)
    // ─────────────────────────────────────────────────────────────────────────

    public static IEnumerable<(string Name, string Code)> ExtractSources(string elementText)
    {
        foreach (Match m in SourceRegex().Matches(elementText))
        {
            var name = m.Groups["Name"].Value;
            var text = m.Groups["Text"].Value.Trim();
            yield return (name, StripSharpPrefix(text));
        }
    }

    public static string BuildXppClass(string elementText)
    {
        var sources = ExtractSources(elementText).ToList();
        if (sources.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var decl = sources.FirstOrDefault(s => s.Name == "classDeclaration");

        if (decl != default)
        {
            // Strip trailing closing brace from declaration
            var body = decl.Code;
            var lastBrace = body.LastIndexOf('}');
            if (lastBrace >= 0) body = body[..lastBrace].TrimEnd();
            sb.AppendLine(body);
            sb.AppendLine();
        }

        const string indent = "    ";
        foreach (var (name, code) in sources.Where(s => s.Name != "classDeclaration"))
        {
            sb.AppendLine();
            foreach (var line in code.Split('\n'))
                sb.AppendLine(string.IsNullOrWhiteSpace(line) ? "" : indent + line.TrimEnd('\r'));
        }

        sb.Append('}');
        return sb.ToString();
    }

    public static string BuildXppJob(string elementText)
    {
        var sb = new StringBuilder();
        foreach (var (_, code) in ExtractSources(elementText))
            sb.AppendLine(code);
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static List<string> SplitByDelimiter(string content)
    {
        var parts = new List<string>();
        int prev = 0;
        int pos;
        while ((pos = content.IndexOf(Delimiter, prev, StringComparison.Ordinal)) >= 0)
        {
            parts.Add(content[prev..pos]);
            prev = pos + Delimiter.Length;
        }
        parts.Add(content[prev..]);
        return parts;
    }

    private static XpoItem? ParseElement(string text, string fileHeader, string sourcePath)
    {
        var m = ElementHeaderRegex().Match(text);
        if (!m.Success)
        {
            if (text.Contains("#KERNDOC:"))
                Console.Error.WriteLine($"[warn] Documentation element skipped in {sourcePath}");
            else if (text.Trim().Length > 0)
                Console.Error.WriteLine($"[warn] Unrecognized element in {sourcePath}: {text[..Math.Min(60, text.Length)].Trim()}");
            return null;
        }

        var tag     = m.Groups["Tag"].Value;
        var rawType = m.Groups["Type"].Value;
        var name    = m.Groups["Name"].Value;
        var subTag  = ResolveSubTag(tag, text);

        var type = XpoTypeRegistry.FindByTag(tag, subTag);
        if (type is null) return null;

        return new XpoItem
        {
            Name           = name,
            RawType        = rawType,
            Type           = type,
            Text           = text,
            FileHeader     = fileHeader,
            SourceFilePath = sourcePath,
        };
    }

    private static string ResolveSubTag(string tag, string text) => tag switch
    {
        "PRN" => PrnSubTagRegex().Match(text).Groups["SubTag"].Value,
        "FTM" => FtmSubTagRegex().Match(text).Groups["SubTag"].Value,
        _     => ""
    };

    private static List<XpoNode> ExtractGroupNodes(XpoItem project)
    {
        var root = new XpoNode { Name = "", Path = [], Project = project };
        var groups = new List<XpoNode> { root };
        var current = root;

        // Use a StringBuilder for each group's accumulated text (O(n) vs O(n²))
        var textBuilders = new Dictionary<XpoNode, StringBuilder> { [root] = new() };

        foreach (Match gm in GroupRegex().Matches(project.Text))
        {
            if (gm.Value.StartsWith("GROUP"))
            {
                var groupName = gm.Groups["Name"].Value;
                var child = new XpoNode
                {
                    Name    = groupName,
                    Path    = [.. current.Path, groupName],
                    Parent  = current,
                    Project = project,
                };
                groups.Add(child);
                textBuilders[child] = new StringBuilder();
                current = child;
            }
            else // ENDGROUP
            {
                if (current.Parent is not null)
                    current = current.Parent;
            }
        }

        // Flush accumulated text
        foreach (var (node, sb) in textBuilders)
            node.Text = sb.ToString();

        // Extract leaf nodes from each group
        var allNodes = new List<XpoNode>();
        foreach (var group in groups)
        {
            foreach (Match nm in NodeBlockRegex().Matches(group.Text))
            {
                var body = nm.Groups["Body"].Value;
                var nodeName  = NodeNameRegex().Match(body).Groups["Name"].Value;
                var nodeType  = int.TryParse(NodeTypeRegex().Match(body).Groups["v"].Value, out var nt) ? nt : 0;
                var utilType  = int.TryParse(UtilTypeRegex().Match(body).Groups["v"].Value, out var ut) ? ut : 0;

                allNodes.Add(new XpoNode
                {
                    Name     = nodeName,
                    Text     = body.Trim(),
                    Path     = group.Path,
                    NodeType = nodeType,
                    UtilType = utilType,
                    Parent   = group,
                    Project  = project,
                });
            }
        }

        return allNodes;
    }

    private static string StripSharpPrefix(string code)
    {
        var lines = code.Split('\n');
        var sb = new StringBuilder(code.Length);
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            // Remove leading whitespace + '#' prefix (AX XPO source encoding)
            var stripped = System.Text.RegularExpressions.Regex.Replace(line, @"\s*?#(.*)", "$1");
            if (!string.IsNullOrWhiteSpace(stripped))
                sb.AppendLine(stripped);
            else
                sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static Encoding DetectEncoding(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> bom = stackalloc byte[4];
        var read = fs.Read(bom);
        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;
        return Encoding.UTF8;
    }
}
