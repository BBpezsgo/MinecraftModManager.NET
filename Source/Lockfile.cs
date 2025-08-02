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
    public static async Task<ImmutableArray<LockfileError>> GetLockfileErrors(string modsDirectory, IReadOnlyList<ModlistLockEntry> modlistLock, bool print, CancellationToken ct)
    {
        List<InstalledComponent> installedMods = [];
        var errors = ImmutableArray.CreateBuilder<LockfileError>();

        ProgressBar progressBar = new();

        if (Directory.Exists(modsDirectory))
        {
            string[] array = Directory.GetFiles(modsDirectory, "*.jar");
            for (int i = 0; i < array.Length; i++)
            {
                string file = array[i];

                progressBar.Report(Path.GetFileName(file), i, array.Length);

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
                    if (print) Log.Error($"File {Path.GetFileName(file)} does not exists in the lockfile");
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
                    if (print) Log.Error($"File {Path.GetFileName(file)} checksum mismatched");
                    continue;
                }
            }
        }

        for (int i = 0; i < modlistLock.Count; i++)
        {
            ModlistLockEntry lockEntry = modlistLock[i];

            progressBar.Report(lockEntry.Name ?? lockEntry.Id, i, modlistLock.Count);

            string file = Path.GetFullPath(Path.Combine(modsDirectory, lockEntry.FileName));

            if (!File.Exists(file))
            {
                errors.Add(new LockfileError()
                {
                    Status = LockfileStatus.NotExists,
                    File = file,
                    Entry = lockEntry,
                });
                if (print) Log.Error($"File {file} does not exists");
                continue;
            }
        }

        progressBar.Dispose();

        return errors.ToImmutable();
    }
}
