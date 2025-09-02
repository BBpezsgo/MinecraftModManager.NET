namespace MMM;

public class GenericComponent : IMod
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public string? Name { get; init; }
}
