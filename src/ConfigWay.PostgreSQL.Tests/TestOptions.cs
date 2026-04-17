namespace Kododo.ConfigWay.PostgreSQL.Tests;

internal class SimpleOptions
{
    public string Name  { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

internal class NestedOptions
{
    public string       TopLevel { get; set; } = string.Empty;
    public ChildOptions Child    { get; set; } = new();
}

internal class ChildOptions
{
    public string ChildProp { get; set; } = string.Empty;
}


