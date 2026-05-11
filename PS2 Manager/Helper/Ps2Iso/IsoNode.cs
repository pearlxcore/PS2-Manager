public class IsoNode
{
    public string Name { get; }
    public bool IsDirectory { get; }
    public List<IsoNode> Children { get; } = new();

    public IsoNode(string name, bool isDir)
    {
        Name = name;
        IsDirectory = isDir;
    }
}
