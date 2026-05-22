namespace XpoFast.Core;

public sealed class XpoItem
{
    public required string Name { get; init; }
    public required string RawType { get; init; }
    public required XpoType Type { get; init; }
    // Element text without "***Element: " delimiter; ends with newline(s)
    public required string Text { get; init; }
    // File header without trailing delimiter; ends with blank line "\r\n\r\n"
    public required string FileHeader { get; init; }
    public required string SourceFilePath { get; init; }
    // Populated for PRN elements: group nodes referenced by this project
    public List<XpoNode> GroupNodes { get; set; } = [];

    public override string ToString() => $"{Type.FilePrefix}_{Name}";
}
