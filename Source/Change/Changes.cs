using System.Collections.Immutable;

namespace MMM;

public readonly struct Changes
{
    public readonly required ImmutableArray<ModInstallInfo> Install { get; init; }
    public readonly required ImmutableArray<ModUninstallInfo> Uninstall { get; init; }

    public bool IsEmpty => Install.IsEmpty && Uninstall.IsEmpty;
}
