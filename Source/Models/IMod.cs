namespace MMM;

public interface IMod
{
    string Id { get; }
    string Version { get; }
    string? Name { get; }
}