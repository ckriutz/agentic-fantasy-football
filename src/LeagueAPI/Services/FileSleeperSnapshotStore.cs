using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using LeagueAPI.Configuration;
using LeagueAPI.Models;

namespace LeagueAPI.Services;

public sealed class FileSleeperSnapshotStore(
    IHostEnvironment hostEnvironment,
    IOptions<SleeperSyncOptions> sleeperSyncOptions)
{
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly SleeperSyncOptions _sleeperSyncOptions = sleeperSyncOptions.Value;

    public async Task<SleeperSnapshot> SavePlayersSnapshotAsync(
        string payload,
        DateTimeOffset fetchedAtUtc,
        CancellationToken cancellationToken)
    {
        var snapshotDirectory = ResolvePath(_sleeperSyncOptions.SnapshotDirectory);
        Directory.CreateDirectory(snapshotDirectory);

        var fileName = $"players-{fetchedAtUtc:yyyy-MM-dd}.json";
        var absolutePath = Path.Combine(snapshotDirectory, fileName);
        await File.WriteAllTextAsync(absolutePath, payload, cancellationToken);

        var relativePath =
            Path.GetRelativePath(_hostEnvironment.ContentRootPath, absolutePath)
                .Replace('\\', '/');

        return new SleeperSnapshot
        {
            FileName = fileName,
            RelativePath = relativePath,
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
        };
    }

    private string ResolvePath(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_hostEnvironment.ContentRootPath, configuredPath);
    }
}
