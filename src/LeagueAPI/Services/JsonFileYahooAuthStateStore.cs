using System.Text.Json;
using LeagueAPI.Configuration;
using LeagueAPI.Models;
using Microsoft.Extensions.Options;

namespace LeagueAPI.Services;

public sealed class JsonFileYahooAuthStateStore(
    IHostEnvironment hostEnvironment,
    IOptions<YahooOAuthOptions> yahooOAuthOptions)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly YahooOAuthOptions _yahooOAuthOptions = yahooOAuthOptions.Value;

    public async Task<YahooOAuthState> GetStateAsync(CancellationToken cancellationToken)
    {
        var stateFilePath = ResolvePath(_yahooOAuthOptions.StateFilePath);

        if (!File.Exists(stateFilePath))
        {
            return new YahooOAuthState();
        }

        var json = await File.ReadAllTextAsync(stateFilePath, cancellationToken);
        return JsonSerializer.Deserialize<YahooOAuthState>(json, SerializerOptions) ?? new YahooOAuthState();
    }

    public async Task SaveStateAsync(YahooOAuthState state, CancellationToken cancellationToken)
    {
        var stateFilePath = ResolvePath(_yahooOAuthOptions.StateFilePath);
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
