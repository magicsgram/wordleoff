﻿using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WordleOff.Shared.Games;

namespace WordleOff.Server.Hubs;

public class WordleOffContext : DbContext
{
  public DbSet<GameSession>? GameSessions { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

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
  }

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    String databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")!;
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