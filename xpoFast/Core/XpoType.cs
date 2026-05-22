namespace XpoFast.Core;

public sealed record XpoType(
    int NodeType,
    int UtilType,
    string Tag,
    string SubTag,
    string FilePrefix,
    string[] AotPath,
    string OneLevelAotPath);
