using System.Text.Json;
using Microsoft.Extensions.Options;
using PlayerDatabase.Configuration;
using PlayerDatabase.Models;

namespace PlayerDatabase.Services;

public sealed class JsonFileSyncStateStore(
    IHostEnvironment hostEnvironment,
    IOptions<SleeperSyncOptions> sleeperSyncOptions)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly SleeperSyncOptions _sleeperSyncOptions = sleeperSyncOptions.Value;

    public async Task<SleeperSyncState> GetLatestStateAsync(CancellationToken cancellationToken)
    {
        var stateFilePath = ResolvePath(_sleeperSyncOptions.StateFilePath);

        if (!File.Exists(stateFilePath))
        {
            return new SleeperSyncState();
        }

        var json = await File.ReadAllTextAsync(stateFilePath, cancellationToken);
        return JsonSerializer.Deserialize<SleeperSyncState>(json, SerializerOptions) ?? new SleeperSyncState();
    }

    public async Task SaveStateAsync(SleeperSyncState state, CancellationToken cancellationToken)
    {
        var stateFilePath = ResolvePath(_sleeperSyncOptions.StateFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);

        var json = JsonSerializer.Serialize(state, SerializerOptions);
        await File.WriteAllTextAsync(stateFilePath, json, cancellationToken);
    }

    private string ResolvePath(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_hostEnvironment.ContentRootPath, configuredPath);
    }
}
