using System.Collections.Immutable;

namespace MMM;

public enum LockfileStatus
{
    NotTracked,
    ChecksumMismatch,
    NotExists,
}

public readonly struct LockfileError
{
    public required LockfileStatus Status { get; init; }
    public required string File { get; init; }
    public required ModlistLockEntry? Entry { get; init; }
}

public static class Lockfile
{
    public static async Task<ImmutableArray<LockfileError>> GetLockfileErrors(string modsDirectory, IEnumerable<ModlistLockEntry> modlistLock, CancellationToken ct)
    {
        List<InstalledComponent> installedMods = [];
        var errors = ImmutableArray.CreateBuilder<LockfileError>();

        foreach (string file in Directory.GetFiles(modsDirectory, "*.jar"))
        {
            string hash = await Utils.ComputeHash1(file, ct);

            var lockEntry = modlistLock.FirstOrDefault(v => v.FileName == Path.GetFileName(file));

            if (lockEntry is null)
            {
                errors.Add(new LockfileError()
                {
                    Status = LockfileStatus.NotTracked,
                    File = file,
                    Entry = null,
                });
                continue;
            }

            if (hash != lockEntry.Hash)
            {
                errors.Add(new LockfileError()
                {
                    Status = LockfileStatus.ChecksumMismatch,
                    File = file,
                    Entry = lockEntry,
                });
                continue;
            }
        }

        foreach (var lockEntry in modlistLock)
        {
            string file = Path.GetFullPath(Path.Combine(modsDirectory, lockEntry.FileName));
            if (!File.Exists(file))
            {
                errors.Add(new LockfileError()
                {
                    Status = LockfileStatus.NotExists,
                    File = file,
                    Entry = lockEntry,
                });
                continue;
            }
        }

        return errors.ToImmutable();
    }

}
