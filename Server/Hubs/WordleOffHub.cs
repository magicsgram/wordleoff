using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using WordleOff.Shared.Games;

namespace WordleOff.Server.Hubs;

public class WordleOffHub : Hub
{
  private static readonly Int32 DbRetryCount = 20;

  private static Boolean initialized = false;
  private static readonly Random random = new();

  private WordleOffContext dbCtx = new();

  private static IHubCallerClients? latestClients;

  private static System.Timers.Timer? removeDisconnectedPlayersTimer;
  private static System.Timers.Timer? removeExpiredSessionsTimer;

  public WordleOffHub() : base()
  {
    if (!initialized)
    {
      initialized = true;
    }
  }

  public static void StaticInitialize()
  { // Do these before ever creating WordleOffHub. (to prevent race conditions)
    if (removeDisconnectedPlayersTimer is null)
    {
      removeDisconnectedPlayersTimer = new(5000); // Every 5 seconds
      removeDisconnectedPlayersTimer.Elapsed += RemoveDisconnectedPlayers;
      removeDisconnectedPlayersTimer.AutoReset = true;
      removeDisconnectedPlayersTimer.Enabled = true;
      removeDisconnectedPlayersTimer.Start();
    }

    if (removeExpiredSessionsTimer is null)
    {
      removeExpiredSessionsTimer = new(300000); // Every 5 minutes
      removeExpiredSessionsTimer.Elapsed += RemoveExpiredSessions;
      removeExpiredSessionsTimer.AutoReset = true;
      removeExpiredSessionsTimer.Enabled = true;
      removeExpiredSessionsTimer.Start();
    }
  }

  protected override void Dispose(Boolean disposing)
  {
    base.Dispose(disposing);
    dbCtx.Dispose();
  }


  #region Received from Client

  public async Task ClientCreateNewSession()
  {
    await DBOpsAsync(async () =>
    {
      GameSession newGameSession = CreateNewSession();
      await dbCtx.GameSessions.AddAsync(newGameSession);
      SessionStat stat = await RetrieveOrCreateSessionStatAsync(dbCtx, "TotalSessionsCreated");
      ++stat.Count;
      await SaveGameSessionToDbAsync();
      await Clients.Caller.SendAsync("NewSessionCreated", newGameSession.SessionId, newGameSession.SpectatorKey);
    });
  }

  public async Task ClientResetCurrentSession(String sessionId)
  {
    await DBOpsAsync(async () =>
    {
      GameSession? gameSession = GetGameSession(sessionId);
      if (gameSession is null)
      {
        await SendJoinErrorAsync(ServerJoinError.SessionNotFound);
        return;
      }

      gameSession.ResetGame();
      await SaveGameSessionToDbAsync(gameSession);

      await SendCurrentAnswerAsync(gameSession);
      await SendFullGameStateAsync(gameSession);
    });
  }

  public async Task ClientSearchSession(String sessionId, String spectatorKey)
  {
    GameSession? gameSession = GetGameSession(sessionId);
    if (gameSession is not null)
      await Clients.Caller.SendAsync("ServerSessionFindResult", true, gameSession.SpectatorKey == spectatorKey, DateTimeOffset.UtcNow);
    else
      await Clients.Caller.SendAsync("ServerSessionFindResult", false, false, DateTimeOffset.UtcNow);
  }

  public async Task ClientConnectNew(String sessionId, String clientGuid, String newPlayerName, Boolean restore, Boolean requestFullWords)
  {
    String? oldSessionId = await GetSessionIdFromConnectionId(Context.ConnectionId);
    if (oldSessionId is not null && oldSessionId != sessionId)
    {
      await RemoveConnectionIdToSessionId(Context.ConnectionId);
      await DBOpsAsync(async () =>
      {
        GameSession? oldGameSession = GetGameSession(oldSessionId);
        if (oldGameSession is not null)
        {
          oldGameSession.DisconnectPlayer(Context.ConnectionId);
          await SaveGameSessionToDbAsync(oldGameSession);
        }
      });
    }

    await DBOpsAsync(async () =>
    {
      GameSession? gameSession = GetGameSession(sessionId);
      if (gameSession is null)
      {
        await SendJoinErrorAsync(ServerJoinError.SessionNotFound);
        return;
      }
      var result = gameSession.AddPlayer(Context.ConnectionId, clientGuid, newPlayerName, restore);
      switch (result)
      {
        case AddPlayerResult.Success:
        case AddPlayerResult.ConnectionRestored:
          await dbCtx.ConnectionIdToSessionIds.AddAsync(new(Context.ConnectionId, gameSession.SessionId));
          await SaveGameSessionToDbAsync(gameSession);
          await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
          if (requestFullWords)
            await SendFullWordsCompressedAsync();
          await SendCurrentAnswerAsync(gameSession, false);
          await SendFullGameStateAsync(gameSession);
          break;

        case AddPlayerResult.PlayerNameExist:
          await SendJoinErrorAsync(ServerJoinError.NameTaken);
          break;

        case AddPlayerResult.PlayerMaxed:
          await SendJoinErrorAsync(ServerJoinError.SessionFull);
          break;

        case AddPlayerResult.CannotRestore:
          await SendJoinErrorAsync(ServerJoinError.CannotRestore);
          break;

        default:
          // TODO: Send an error message
          break;
      }
    });
  }

  public async Task ClientConnectAsSpectatorOrStreamer(String sessionId)
  {
    GameSession? gameSession = GetGameSession(sessionId);
    if (gameSession is null)
    {
      await SendJoinErrorAsync(ServerJoinError.SessionNotFound);
      return;
    }
    await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    await SendCurrentAnswerAsync(gameSession, false);
    await SendFullGameStateAsync(gameSession);
  }

  public async Task ClientReconnect(String sessionId, String playerName, Boolean spectatorOrStreamerMode)
  {
    await DBOpsAsync(async () =>
    {
      GameSession? gameSession = GetGameSession(sessionId);
      if (gameSession is null)
      {
        await SendJoinErrorAsync(ServerJoinError.SessionNotFound);
        return;
      }
      if (!spectatorOrStreamerMode) // Check if Spectator
        if (gameSession.ReconnectPlayer(playerName, Context.ConnectionId)) // Actual Player
        {
          await dbCtx.ConnectionIdToSessionIds.AddAsync(new(Context.ConnectionId, gameSession.SessionId));
          await SaveGameSessionToDbAsync(gameSession);
        }
      await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
      await SendFullGameStateAsync(gameSession);
    });
  }

  public async Task ClientSubmitGuess(String sessionId, String playerName, String guess)
  {
    await DBOpsAsync(async () =>
    {
      GameSession? gameSession = GetGameSession(sessionId);
      if (gameSession is not null)
      {
        Int32 placement = gameSession.EnterGuess(playerName, guess);
        if (placement > 0)
        {
          String decryptedWord = EncryptDecrypt.XorDecryptPadding(guess);
          WordStat? wordStat = dbCtx.WordStats.Find(decryptedWord);
          if (wordStat is null)
          {
            wordStat = new WordStat(decryptedWord);
            dbCtx.WordStats.Add(wordStat);
          }
          wordStat.WordSubmitted(placement);
          await SaveGameSessionToDbAsync(gameSession);
          await SendCurrentAnswerAsync(gameSession);
          await SendFullGameStateAsync(gameSession);
        }
      }
    });
  }

  public async Task ClientAdminInfo(String adminKey)
  {
    if (adminKey == Environment.GetEnvironmentVariable("ADMIN_KEY"))
    {
      await Clients.Caller.SendAsync("SeverAdminInfoSessionStats", await dbCtx.SessionStats.ToListAsync());
      await Clients.Caller.SendAsync("ServerAdminInfoGameSessions", await dbCtx.GameSessions.ToListAsync());
    }
  }

  public async Task ClientAdminWordStats(String adminKey)
  {
    if (adminKey == Environment.GetEnvironmentVariable("ADMIN_KEY"))
      await Clients.Caller.SendAsync("ServerAdminInfoWordStats", await dbCtx.WordStats.ToListAsync());
  }

  #endregion


  #region Send to Client

  public async Task SendFullWordsCompressedAsync() => await Clients.Caller.SendAsync("ServerFullWordsCompressed", WordsService.CompressedFullWordsBytes);

  public async Task SendCurrentAnswerAsync(GameSession gameSession, Boolean sendToWholeGroup = true)
  {
    String encrypted = EncryptDecrypt.XorEncryptPadding(gameSession.CurrentAnswer);
    if (sendToWholeGroup)
      await Clients.Group(gameSession.SessionId).SendAsync("ServerCurrentAnswer", encrypted);
    else
      await Clients.Caller.SendAsync("ServerCurrentAnswer", encrypted);
  }

  public async Task SendFullGameStateAsync(GameSession gameSession, Boolean sendToWholeGroup = true)
  {
    if (sendToWholeGroup)
      await Clients.Group(gameSession.SessionId).SendAsync("ServerPlayerData", gameSession.PlayerDataDictionary);
    else
      await Clients.Caller.SendAsync("ServerPlayerData", gameSession.PlayerDataDictionary);
  }

  public async Task SendJoinErrorAsync(ServerJoinError error) => await Clients.Caller.SendAsync("ServerJoinError", error);

  #endregion


  #region Other Server Codes

  public GameSession CreateNewSession()
  {
    String newSessionId = GetNewGameSessionId();
    GameSession gameSession = new GameSession(newSessionId);
    return gameSession;
  }

  private async Task SaveGameSessionToDbAsync(GameSession? gameSession = null)
  {
    try
    {
      if (gameSession is not null)
        dbCtx.Update(gameSession);
      await dbCtx.SaveChangesAsync();
    }
    catch (Exception) { }
  }

  private String GetNewGameSessionId()
  {
    String newSessionId;
    for (; ; )
    {
      List<String> segments = new(16);
      for (Int32 i = 0; i < 3; ++i)
      {
        // Three digit numbers w/ the same number in each digit are considered
        // bad omen in some countries. Just roll the dice again.
        String threeDigitNumber;
        do
        {
          threeDigitNumber = random.Next(100, 1000).ToString("000");
          if (!(threeDigitNumber[0] == threeDigitNumber[1] && threeDigitNumber[0] == threeDigitNumber[2]))
            break;
        } while (true);
        segments.Add(threeDigitNumber);
      }
      newSessionId = String.Join("-", segments);
      if (!GameSessionExist(newSessionId))
        break;
    }
    return newSessionId;
  }

  private GameSession? GetGameSession(String sessionId) => dbCtx.GameSessions.Find(sessionId);

  private Boolean GameSessionExist(String sessionId) => GetGameSession(sessionId) is not null;

  public static void RemoveDisconnectedPlayers(Object? sender, System.Timers.ElapsedEventArgs e)
  {
    Task.Run(async () =>
    {
      WordleOffContext tempCtx = new();
      if (tempCtx.GameSessions is not null)
      {
        var gameSessionList = await tempCtx.GameSessions.ToListAsync();
        foreach (GameSession gameSession in gameSessionList)
        {
          if (gameSession is not null)
          {
            await DBOpsAsync(async () =>
            {
              String sessionId = gameSession.SessionId;
              GameSession? tempSession = tempCtx.GameSessions.Find(sessionId);
              if (tempSession is not null && tempSession.RemoveDisconnectedPlayer())
              {
                tempCtx.Update(tempSession);
                await tempCtx.SaveChangesAsync();
                if (latestClients is not null)
                {
                  WordleOffHub newHub = new();
                  newHub.Clients = latestClients;
                  await newHub.SendFullGameStateAsync(tempSession);
                }
              }
            });
          }
        }
      }
      await tempCtx.DisposeAsync();
    });
  }

  public static void RemoveExpiredSessions(Object? sender, System.Timers.ElapsedEventArgs e)
  {
    GC.Collect(); // Necessary? It helped to keep the app unber 512MB
    Task.Run(async () =>
    {
      WordleOffContext tempCtx = new();
      if (tempCtx.GameSessions is not null)
      {
        await DBOpsAsync(async () =>
        {
          UInt64 totalPlayers = 0;
          UInt64 totalSessionTimeSeconds = 0;
          UInt64 totalGamesPlayed = 0;
          UInt64 totalGameTimeSeconds = 0;
          List<Int32> maxPlayersList = new();

          List<GameSession> expiredSessions = (await tempCtx.GameSessions.ToListAsync()).Where(x => x.SessionExpired).ToList();
          foreach (GameSession gameSession in expiredSessions)
          {
            gameSession.PrepForRemoval();
            totalPlayers += (UInt64)gameSession.TotalPlayersConnected;
            totalSessionTimeSeconds += (UInt64)Math.Round((gameSession.UpdatedAt - gameSession.CreatedAt).TotalSeconds);
            totalGamesPlayed += (UInt64)gameSession.TotalGamesPlayed;
            totalGameTimeSeconds += (UInt64)gameSession.TotalGameTimeSeconds;
            maxPlayersList.Add(gameSession.MaxPlayersConnected);

            tempCtx.GameSessions.Remove(gameSession);
            foreach (ConnectionIdToSessionId conn in await tempCtx.ConnectionIdToSessionIds.Where(x => x.SessionId == gameSession.SessionId).ToListAsync())
              tempCtx.ConnectionIdToSessionIds.Remove(conn);
          }
          SessionStat totalPlayersStat = await RetrieveOrCreateSessionStatAsync(tempCtx, "TotalPlayers");
          totalPlayersStat.Count += totalPlayers;

          SessionStat totalSessionTimeSecondsStat = await RetrieveOrCreateSessionStatAsync(tempCtx, "TotalSessionTimeSeconds");
          totalSessionTimeSecondsStat.Count += totalSessionTimeSeconds;

          SessionStat totalGamesPlayedStat = await RetrieveOrCreateSessionStatAsync(tempCtx, "TotalGamesPlayed");
          totalGamesPlayedStat.Count += totalGamesPlayed;

          SessionStat totalGameTimeSecondsStat = await RetrieveOrCreateSessionStatAsync(tempCtx, "TotalGameTimeSeconds");
          totalGameTimeSecondsStat.Count += totalGameTimeSeconds;

          List<SessionStat> maxPlayerCountStatList = new(GameSession.MaxPlayers + 1);
          for (Int32 i = 0; i <= GameSession.MaxPlayers; ++i)
            maxPlayerCountStatList.Add(await RetrieveOrCreateSessionStatAsync(tempCtx, $"MaxPlayerCount_{i}"));
          foreach (Int32 maxPlayerCount in maxPlayersList)
            ++maxPlayerCountStatList[maxPlayerCount].Count;

          await tempCtx.SaveChangesAsync();
        });
      }
      await tempCtx.DisposeAsync();
    });
  }

  public static async Task<SessionStat> RetrieveOrCreateSessionStatAsync(WordleOffContext localCtx, String category)
  {
    SessionStat? stat = await localCtx.SessionStats.FindAsync(category);
    if (stat is null)
    {
      stat = new(category);
      await localCtx.SessionStats.AddAsync(stat);
    }
    return stat;
  }

  public async override Task OnDisconnectedAsync(Exception? exception)
  {
    await base.OnDisconnectedAsync(exception);
    String? sessionId = await RemoveConnectionIdToSessionId(Context.ConnectionId);
    if (sessionId is not null)
      await DBOpsAsync(async () =>
      {
        GameSession? gameSession = GetGameSession(sessionId);
        if (gameSession is not null)
        {
          gameSession.DisconnectPlayer(Context.ConnectionId);
          await SaveGameSessionToDbAsync(gameSession);
          await SendFullGameStateAsync(gameSession);
        }
      });
    latestClients = Clients;
  }

  public static void SleepRandomForDbRetry() => Thread.Sleep(random.Next(1, 150));

  public async Task<String?> GetSessionIdFromConnectionId(String connectionId)
    => (await dbCtx.ConnectionIdToSessionIds.FindAsync(connectionId))?.SessionId;

  public async Task<String?> RemoveConnectionIdToSessionId(String connectionId)
  {
    String? sessionId = null;
    await DBOpsAsync(async () =>
    {
      ConnectionIdToSessionId? map = await dbCtx.ConnectionIdToSessionIds.FindAsync(connectionId);
      if (map is not null)
      {
        sessionId = map.SessionId;
        dbCtx.ConnectionIdToSessionIds.Remove(map);
        await dbCtx.SaveChangesAsync();
      }
    });
    return sessionId;
  }

  public async static Task DBOpsAsync(Func<Task> func)
  {
    Int32 tryCount = 0;
    while (tryCount < DbRetryCount)
    {
      try
      {
        await func();
        break;
      }
      catch (DbUpdateConcurrencyException)
      {
        SleepRandomForDbRetry();
        ++tryCount;
      }
      catch (Exception) { break; }
    }
  }

  #endregion
}
