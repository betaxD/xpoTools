namespace XpoFast.Core;

public sealed class XpoNode
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public string[] Path { get; set; } = [];
    public int NodeType { get; set; }
    public int UtilType { get; set; }
    public XpoNode? Parent { get; set; }
    public XpoItem? Project { get; set; }
}
