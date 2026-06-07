using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LeagueAPI.Data;

public sealed class LeagueApiDbContextFactory : IDesignTimeDbContextFactory<LeagueApiDbContext>
{
    public LeagueApiDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["DBConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "Host=localhost;Database=leagueapi;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<LeagueApiDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new LeagueApiDbContext(optionsBuilder.Options);
    }
}
