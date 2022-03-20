using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WordleOff.Shared.Games;

namespace WordleOff.Server.Hubs;

public class WordleOffContext : DbContext
{
  public DbSet<GameSession> GameSessions => Set<GameSession>();
  public DbSet<ConnectionIdToSessionId> ConnectionIdToSessionIds => Set<ConnectionIdToSessionId>();
  public DbSet<WordStat> WordStats => Set<WordStat>();
  public DbSet<SessionStat> SessionStats => Set<SessionStat>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // GameSessions related
    modelBuilder.Entity<GameSession>()
      .Property(x => x.PlayerDataDictionary)
      .HasConversion(
        v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
        v => JsonSerializer.Deserialize<Dictionary<String, PlayerData>>(v, new JsonSerializerOptions())!
      )
      .HasColumnType("jsonb");

    modelBuilder.Entity<GameSession>()
     .Property(x => x.PastAnswers)
     .HasConversion(
       v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
       v => JsonSerializer.Deserialize<Queue<String>>(v, new JsonSerializerOptions())!
     )
     .HasColumnType("jsonb");

    modelBuilder.Entity<GameSession>()
      .UseXminAsConcurrencyToken();

    modelBuilder.Entity<GameSession>()
      .HasIndex(x => x.SessionId);

    // ConnectionIdToSessionIds related
    modelBuilder.Entity<ConnectionIdToSessionId>()
      .HasIndex(x => x.ConnectionId)
      .IsUnique();

    modelBuilder.Entity<ConnectionIdToSessionId>()
      .UseXminAsConcurrencyToken();

    // WordStats related
    modelBuilder.Entity<WordStat>()
      .HasIndex(x => x.Word)
      .IsUnique();

    modelBuilder.Entity<WordStat>()
      .UseXminAsConcurrencyToken();

    // SessionStats related
    modelBuilder.Entity<SessionStat>()
      .HasIndex(x => x.Category)
      .IsUnique();

    modelBuilder.Entity<SessionStat>()
      .UseXminAsConcurrencyToken();
  }

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    String? databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (databaseUrl is null)
      databaseUrl = "postgres://postgres:postgres@localhost:5432/wordleoffdb";
    Uri uri = new(databaseUrl);
    String[] userInfoPart = uri.UserInfo.Split(':');

    var builder = new Npgsql.NpgsqlConnectionStringBuilder
    {
      Username = userInfoPart[0],
      Password = userInfoPart[1],
      Host = uri.Host,
      Port = uri.Port,
      Database = uri.LocalPath.TrimStart('/'),
      SslMode = Npgsql.SslMode.Require,
      TrustServerCertificate = true
    };
    String connectionString = builder.ToString();
    optionsBuilder.UseNpgsql(connectionString);
  }
}