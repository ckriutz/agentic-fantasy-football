using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using PlayerDatabase.Models;

namespace PlayerDatabase.Services;

public sealed class SqlServerPlayerCatalogStore(
    IConfiguration configuration) : IPlayerCatalogReader, IPlayerCatalogPersistence
{
    private const string SchemaSql = """
        IF OBJECT_ID(N'dbo.SleeperSyncRuns', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SleeperSyncRuns
            (
                SyncRunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SleeperSyncRuns PRIMARY KEY,
                StartedAtUtc DATETIMEOFFSET NOT NULL,
                CompletedAtUtc DATETIMEOFFSET NULL,
                Status NVARCHAR(32) NOT NULL,
                RecordCount INT NULL,
                SnapshotFileName NVARCHAR(260) NULL,
                SnapshotRelativePath NVARCHAR(520) NULL,
                PayloadSha256 NVARCHAR(64) NULL,
                ErrorMessage NVARCHAR(MAX) NULL
            );
        END;

        IF OBJECT_ID(N'dbo.Players', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Players
            (
                SleeperPlayerId NVARCHAR(50) NOT NULL CONSTRAINT PK_Players PRIMARY KEY,
                YahooId INT NULL,
                FullName NVARCHAR(200) NULL,
                FirstName NVARCHAR(100) NULL,
                LastName NVARCHAR(100) NULL,
                SearchFullNameNormalized NVARCHAR(200) NOT NULL,
                Team NVARCHAR(50) NULL,
                TeamAbbr NVARCHAR(50) NULL,
                Position NVARCHAR(20) NULL,
                FantasyPositionsTokenized NVARCHAR(100) NOT NULL,
                Status NVARCHAR(50) NULL,
                Active BIT NOT NULL,
                Sport NVARCHAR(20) NULL,
                RawJson NVARCHAR(MAX) NOT NULL,
                UpdatedAtUtc DATETIMEOFFSET NOT NULL
            );
        END;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_Players_YahooId'
              AND object_id = OBJECT_ID(N'dbo.Players'))
        BEGIN
            CREATE INDEX IX_Players_YahooId
                ON dbo.Players (YahooId)
                WHERE YahooId IS NOT NULL;
        END;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_Players_SearchFullNameNormalized'
              AND object_id = OBJECT_ID(N'dbo.Players'))
        BEGIN
            CREATE INDEX IX_Players_SearchFullNameNormalized
                ON dbo.Players (SearchFullNameNormalized);
        END;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_Players_TeamAbbr_Position'
              AND object_id = OBJECT_ID(N'dbo.Players'))
        BEGIN
            CREATE INDEX IX_Players_TeamAbbr_Position
                ON dbo.Players (TeamAbbr, Position);
        END;
        """;

    private const string UpsertPlayerSql = """
        UPDATE dbo.Players
        SET
            YahooId = @YahooId,
            FullName = @FullName,
            FirstName = @FirstName,
            LastName = @LastName,
            SearchFullNameNormalized = @SearchFullNameNormalized,
            Team = @Team,
            TeamAbbr = @TeamAbbr,
            Position = @Position,
            FantasyPositionsTokenized = @FantasyPositionsTokenized,
            Status = @Status,
            Active = @Active,
            Sport = @Sport,
            RawJson = @RawJson,
            UpdatedAtUtc = @UpdatedAtUtc
        WHERE SleeperPlayerId = @SleeperPlayerId;

        IF @@ROWCOUNT = 0
        BEGIN
            INSERT INTO dbo.Players
            (
                SleeperPlayerId,
                YahooId,
                FullName,
                FirstName,
                LastName,
                SearchFullNameNormalized,
                Team,
                TeamAbbr,
                Position,
                FantasyPositionsTokenized,
                Status,
                Active,
                Sport,
                RawJson,
                UpdatedAtUtc
            )
            VALUES
            (
                @SleeperPlayerId,
                @YahooId,
                @FullName,
                @FirstName,
                @LastName,
                @SearchFullNameNormalized,
                @Team,
                @TeamAbbr,
                @Position,
                @FantasyPositionsTokenized,
                @Status,
                @Active,
                @Sport,
                @RawJson,
                @UpdatedAtUtc
            );
        END;
        """;

    private readonly IConfiguration _configuration = configuration;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _schemaInitialized;

    public async Task<PlayerRecord?> GetBySleeperIdAsync(string sleeperPlayerId, CancellationToken cancellationToken)
    {
        await EnsureSchemaInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1)
                SleeperPlayerId,
                YahooId,
                FullName,
                FirstName,
                LastName,
                SearchFullNameNormalized,
                Team,
                TeamAbbr,
                Position,
                FantasyPositionsTokenized,
                Status,
                Active,
                Sport,
                RawJson
            FROM dbo.Players
            WHERE SleeperPlayerId = @SleeperPlayerId
              AND Active = 1;
            """;
        command.Parameters.Add(new SqlParameter("@SleeperPlayerId", SqlDbType.NVarChar, 50)
        {
            Value = sleeperPlayerId
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapPlayer(reader) : null;
    }

    public async Task<PlayerRecord?> GetByYahooIdAsync(int yahooId, CancellationToken cancellationToken)
    {
        await EnsureSchemaInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1)
                SleeperPlayerId,
                YahooId,
                FullName,
                FirstName,
                LastName,
                SearchFullNameNormalized,
                Team,
                TeamAbbr,
                Position,
                FantasyPositionsTokenized,
                Status,
                Active,
                Sport,
                RawJson
            FROM dbo.Players
            WHERE YahooId = @YahooId
              AND Active = 1;
            """;
        command.Parameters.Add(new SqlParameter("@YahooId", SqlDbType.Int)
        {
            Value = yahooId
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapPlayer(reader) : null;
    }

    public async Task<IReadOnlyList<PlayerRecord>> QueryAsync(PlayerQuery query, CancellationToken cancellationToken)
    {
        await EnsureSchemaInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@Limit)
                SleeperPlayerId,
                YahooId,
                FullName,
                FirstName,
                LastName,
                SearchFullNameNormalized,
                Team,
                TeamAbbr,
                Position,
                FantasyPositionsTokenized,
                Status,
                Active,
                Sport,
                RawJson
            FROM dbo.Players
            WHERE
                Active = 1
                AND (@NameNormalized = N'' OR SearchFullNameNormalized LIKE N'%' + @NameNormalized + N'%')
                AND (
                    @TeamNormalized = N''
                    OR UPPER(ISNULL(TeamAbbr, N'')) = @TeamNormalized
                    OR UPPER(ISNULL(Team, N'')) = @TeamNormalized
                )
                AND (
                    @PositionNormalized = N''
                    OR UPPER(ISNULL(Position, N'')) = @PositionNormalized
                    OR FantasyPositionsTokenized LIKE N'%|' + @PositionNormalized + N'|%'
                )
            ORDER BY FullName, SleeperPlayerId;
            """;
        command.Parameters.Add(new SqlParameter("@Limit", SqlDbType.Int)
        {
            Value = Math.Clamp(query.Limit, 1, 200)
        });
        command.Parameters.Add(new SqlParameter("@NameNormalized", SqlDbType.NVarChar, 200)
        {
            Value = PlayerRecordFactory.NormalizeName(query.Name)
        });
        command.Parameters.Add(new SqlParameter("@TeamNormalized", SqlDbType.NVarChar, 50)
        {
            Value = PlayerRecordFactory.NormalizeToken(query.Team)
        });
        command.Parameters.Add(new SqlParameter("@PositionNormalized", SqlDbType.NVarChar, 20)
        {
            Value = PlayerRecordFactory.NormalizeToken(query.Position)
        });

        var players = new List<PlayerRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            players.Add(MapPlayer(reader));
        }

        return players;
    }

    public async Task RecordSyncStartedAsync(
        Guid syncRunId,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.SleeperSyncRuns (SyncRunId, StartedAtUtc, Status)
            VALUES (@SyncRunId, @StartedAtUtc, @Status);
            """;
        command.Parameters.Add(new SqlParameter("@SyncRunId", SqlDbType.UniqueIdentifier) { Value = syncRunId });
        command.Parameters.Add(new SqlParameter("@StartedAtUtc", SqlDbType.DateTimeOffset) { Value = startedAtUtc });
        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 32) { Value = "Started" });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task PersistPlayersAsync(
        IReadOnlyCollection<PlayerRecord> players,
        Guid syncRunId,
        DateTimeOffset persistedAtUtc,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertPlayerSql;

        var sleeperPlayerIdParameter = command.Parameters.Add(new SqlParameter("@SleeperPlayerId", SqlDbType.NVarChar, 50));
        var yahooIdParameter = command.Parameters.Add(new SqlParameter("@YahooId", SqlDbType.Int));
        var fullNameParameter = command.Parameters.Add(new SqlParameter("@FullName", SqlDbType.NVarChar, 200));
        var firstNameParameter = command.Parameters.Add(new SqlParameter("@FirstName", SqlDbType.NVarChar, 100));
        var lastNameParameter = command.Parameters.Add(new SqlParameter("@LastName", SqlDbType.NVarChar, 100));
        var searchFullNameParameter = command.Parameters.Add(new SqlParameter("@SearchFullNameNormalized", SqlDbType.NVarChar, 200));
        var teamParameter = command.Parameters.Add(new SqlParameter("@Team", SqlDbType.NVarChar, 50));
        var teamAbbrParameter = command.Parameters.Add(new SqlParameter("@TeamAbbr", SqlDbType.NVarChar, 50));
        var positionParameter = command.Parameters.Add(new SqlParameter("@Position", SqlDbType.NVarChar, 20));
        var fantasyPositionsParameter = command.Parameters.Add(new SqlParameter("@FantasyPositionsTokenized", SqlDbType.NVarChar, 100));
        var statusParameter = command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50));
        var activeParameter = command.Parameters.Add(new SqlParameter("@Active", SqlDbType.Bit));
        var sportParameter = command.Parameters.Add(new SqlParameter("@Sport", SqlDbType.NVarChar, 20));
        var rawJsonParameter = command.Parameters.Add(new SqlParameter("@RawJson", SqlDbType.NVarChar, -1));
        var updatedAtParameter = command.Parameters.Add(new SqlParameter("@UpdatedAtUtc", SqlDbType.DateTimeOffset));

        foreach (var player in players)
        {
            sleeperPlayerIdParameter.Value = player.SleeperPlayerId;
            yahooIdParameter.Value = DbValue(player.YahooId);
            fullNameParameter.Value = DbValue(player.FullName);
            firstNameParameter.Value = DbValue(player.FirstName);
            lastNameParameter.Value = DbValue(player.LastName);
            searchFullNameParameter.Value = player.SearchFullNameNormalized;
            teamParameter.Value = DbValue(player.Team);
            teamAbbrParameter.Value = DbValue(player.TeamAbbr);
            positionParameter.Value = DbValue(player.Position);
            fantasyPositionsParameter.Value = player.FantasyPositionsTokenized;
            statusParameter.Value = DbValue(player.Status);
            activeParameter.Value = player.Active;
            sportParameter.Value = DbValue(player.Sport);
            rawJsonParameter.Value = player.RawJson;
            updatedAtParameter.Value = persistedAtUtc;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordSyncCompletedAsync(SleeperSyncState syncState, CancellationToken cancellationToken)
    {
        await EnsureSchemaInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.SleeperSyncRuns
            SET
                CompletedAtUtc = @CompletedAtUtc,
                Status = @Status,
                RecordCount = @RecordCount,
                SnapshotFileName = @SnapshotFileName,
                SnapshotRelativePath = @SnapshotRelativePath,
                PayloadSha256 = @PayloadSha256,
                ErrorMessage = @ErrorMessage
            WHERE SyncRunId = @SyncRunId;
            """;
        command.Parameters.Add(new SqlParameter("@CompletedAtUtc", SqlDbType.DateTimeOffset)
        {
            Value = syncState.LastSuccessfulSyncAtUtc ?? DateTimeOffset.UtcNow
        });
        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 32)
        {
            Value = syncState.Status
        });
        command.Parameters.Add(new SqlParameter("@RecordCount", SqlDbType.Int)
        {
            Value = DbValue(syncState.RecordCount)
        });
        command.Parameters.Add(new SqlParameter("@SnapshotFileName", SqlDbType.NVarChar, 260)
        {
            Value = DbValue(syncState.SnapshotFileName)
        });
        command.Parameters.Add(new SqlParameter("@SnapshotRelativePath", SqlDbType.NVarChar, 520)
        {
            Value = DbValue(syncState.SnapshotRelativePath)
        });
        command.Parameters.Add(new SqlParameter("@PayloadSha256", SqlDbType.NVarChar, 64)
        {
            Value = DbValue(syncState.PayloadSha256)
        });
        command.Parameters.Add(new SqlParameter("@ErrorMessage", SqlDbType.NVarChar, -1)
        {
            Value = DbValue(syncState.ErrorMessage)
        });
        command.Parameters.Add(new SqlParameter("@SyncRunId", SqlDbType.UniqueIdentifier)
        {
            Value = syncState.SyncRunId ?? throw new InvalidOperationException("A completed sync state must include a sync run ID.")
        });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordSyncFailedAsync(
        Guid syncRunId,
        DateTimeOffset failedAtUtc,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.SleeperSyncRuns
            SET
                CompletedAtUtc = @CompletedAtUtc,
                Status = @Status,
                ErrorMessage = @ErrorMessage
            WHERE SyncRunId = @SyncRunId;
            """;
        command.Parameters.Add(new SqlParameter("@CompletedAtUtc", SqlDbType.DateTimeOffset) { Value = failedAtUtc });
        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 32) { Value = "Failed" });
        command.Parameters.Add(new SqlParameter("@ErrorMessage", SqlDbType.NVarChar, -1) { Value = errorMessage });
        command.Parameters.Add(new SqlParameter("@SyncRunId", SqlDbType.UniqueIdentifier) { Value = syncRunId });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureSchemaInitializedAsync(CancellationToken cancellationToken)
    {
        if (_schemaInitialized)
        {
            return;
        }

        await _initializeLock.WaitAsync(cancellationToken);

        try
        {
            if (_schemaInitialized)
            {
                return;
            }

            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = SchemaSql;
            await command.ExecuteNonQueryAsync(cancellationToken);

            _schemaInitialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString =
            _configuration.GetConnectionString("PlayerDatabase")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:PlayerDatabase must be configured when PlayerCatalog:Mode is SqlServer.");

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static PlayerRecord MapPlayer(SqlDataReader reader)
    {
        var sleeperPlayerId = reader.GetString(reader.GetOrdinal("SleeperPlayerId"));
        var yahooIdOrdinal = reader.GetOrdinal("YahooId");
        var fullNameOrdinal = reader.GetOrdinal("FullName");
        var firstNameOrdinal = reader.GetOrdinal("FirstName");
        var lastNameOrdinal = reader.GetOrdinal("LastName");
        var searchFullNameOrdinal = reader.GetOrdinal("SearchFullNameNormalized");
        var teamOrdinal = reader.GetOrdinal("Team");
        var teamAbbrOrdinal = reader.GetOrdinal("TeamAbbr");
        var positionOrdinal = reader.GetOrdinal("Position");
        var fantasyPositionsOrdinal = reader.GetOrdinal("FantasyPositionsTokenized");
        var statusOrdinal = reader.GetOrdinal("Status");
        var activeOrdinal = reader.GetOrdinal("Active");
        var sportOrdinal = reader.GetOrdinal("Sport");
        var rawJsonOrdinal = reader.GetOrdinal("RawJson");

        var rawJson = reader.GetString(rawJsonOrdinal);
        var sleeperPlayer =
            JsonSerializer.Deserialize<SleeperPlayer>(rawJson)
            ?? throw new InvalidOperationException($"Unable to deserialize stored player payload for {sleeperPlayerId}.");

        return new PlayerRecord
        {
            SleeperPlayerId = sleeperPlayerId,
            YahooId = reader.IsDBNull(yahooIdOrdinal) ? null : reader.GetInt32(yahooIdOrdinal),
            FullName = reader.IsDBNull(fullNameOrdinal) ? null : reader.GetString(fullNameOrdinal),
            FirstName = reader.IsDBNull(firstNameOrdinal) ? null : reader.GetString(firstNameOrdinal),
            LastName = reader.IsDBNull(lastNameOrdinal) ? null : reader.GetString(lastNameOrdinal),
            Team = reader.IsDBNull(teamOrdinal) ? null : reader.GetString(teamOrdinal),
            TeamAbbr = reader.IsDBNull(teamAbbrOrdinal) ? null : reader.GetString(teamAbbrOrdinal),
            Position = reader.IsDBNull(positionOrdinal) ? null : reader.GetString(positionOrdinal),
            FantasyPositions = ParseFantasyPositions(
                reader.IsDBNull(fantasyPositionsOrdinal)
                    ? string.Empty
                    : reader.GetString(fantasyPositionsOrdinal)),
            Status = reader.IsDBNull(statusOrdinal) ? null : reader.GetString(statusOrdinal),
            Active = reader.GetBoolean(activeOrdinal),
            Sport = reader.IsDBNull(sportOrdinal) ? null : reader.GetString(sportOrdinal),
            SearchFullNameNormalized = reader.GetString(searchFullNameOrdinal),
            FantasyPositionsTokenized = reader.IsDBNull(fantasyPositionsOrdinal)
                ? string.Empty
                : reader.GetString(fantasyPositionsOrdinal),
            RawJson = rawJson,
            Data = sleeperPlayer
        };
    }

    private static IReadOnlyList<string> ParseFantasyPositions(string tokenizedFantasyPositions)
    {
        return tokenizedFantasyPositions.Split(
            '|',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static object DbValue<T>(T? value)
    {
        return value is null ? DBNull.Value : value;
    }
}
