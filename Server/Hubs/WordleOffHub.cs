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

  public async Task ClientCreateNewSession(String clientGuid)
  {
    await DBOpsAsync(async () =>
    {
      GameSession newGameSession = CreateNewSession();
      await dbCtx.GameSessions.AddAsync(newGameSession);
      await SaveGameSessionToDbAsync();
      await Clients.Caller.SendAsync("NewSessionCreated", newGameSession.SessionId);
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

  public async Task ClientSearchSession(String sessionId)
    => await Clients.Caller.SendAsync("ServerSessionFindResult", GameSessionExist(sessionId), DateTimeOffset.UtcNow);

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

  public async Task ClientConnectAsSpectator(String sessionId)
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

  public async Task ClientReconnect(String sessionId, String playerName, Boolean spectatorMode)
  {
    await DBOpsAsync(async () =>
    {
      GameSession? gameSession = GetGameSession(sessionId);
      if (gameSession is null)
      {
        await SendJoinErrorAsync(ServerJoinError.SessionNotFound);
        return;
      }
      if (!spectatorMode) // Check if Spectator
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
        if (gameSession.EnterGuess(playerName, guess) == EnterWordResult.Success)
        {
          await SaveGameSessionToDbAsync(gameSession);
          await SendFullGameStateAsync(gameSession);
        }
      }
    });
  }

  public async Task ClientSubmitGuess2(String sessionId, String playerName, String guess) => await ClientSubmitGuess(sessionId, playerName, guess);

  public async Task ClientAdminInfo(String adminKey)
  {
    if (adminKey == Environment.GetEnvironmentVariable("ADMIN_KEY"))
      await Clients.Caller.SendAsync("ServerAdminInfo", await dbCtx.GameSessions.ToListAsync());
  }

  #endregion


  #region Send to Client

  public async Task SendFullWordsCompressedAsync() => await Clients.Caller.SendAsync("ServerFullWordsCompressed", WordsService.CompressedFullWordsBytes);

  public async Task SendCurrentAnswerAsync(GameSession gameSession, Boolean sendToWholeGroup = true)
  {
    String encrypted = EncryptDecrypt.XorEncrypt(gameSession.CurrentAnswer);
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
      List<String> segments = new();
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
    Task.Run(async () =>
    {
      GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
      GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
      WordleOffContext tempCtx = new();
      if (tempCtx.GameSessions is not null)
      {
        await DBOpsAsync(async () =>
        {
          var expiredSessions = await tempCtx.GameSessions.Where(x => x.SessionExpired).ToListAsync();
          foreach (GameSession? gameSession in expiredSessions)
            if (gameSession is not null)
            {
              tempCtx.GameSessions.Remove(gameSession);
              foreach (ConnectionIdToSessionId conn in await tempCtx.ConnectionIdToSessionIds.Where(x => x.SessionId == gameSession.SessionId).ToListAsync())
                tempCtx.ConnectionIdToSessionIds.Remove(conn);
            }
          await tempCtx.SaveChangesAsync();
        });
      }
      await tempCtx.DisposeAsync();
    });
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
