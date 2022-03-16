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

  private static readonly ConcurrentDictionary<String, String> connectionIdSessionIds = new();
  private WordleOffContext dbCtx = new WordleOffContext();

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
      removeDisconnectedPlayersTimer = new(5000);
      removeDisconnectedPlayersTimer.Elapsed += RemoveDisconnectedPlayers;
      removeDisconnectedPlayersTimer.AutoReset = true;
      removeDisconnectedPlayersTimer.Enabled = true;
      removeDisconnectedPlayersTimer.Start();
    }

    if (removeExpiredSessionsTimer is null)
    {
      removeExpiredSessionsTimer = new(60000);
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
    connectionIdSessionIds.Remove(Context.ConnectionId, out String? sessionId);
    if (dbCtx.GameSessions is not null)
    {
      Int32 tryCount = 0;
      while (tryCount < DbRetryCount)
      {
        try
        {
          if (sessionId is not null)
          {
            GameSession? gameSession = GetGameSession(sessionId);
            if (gameSession is not null)
              gameSession.DisconnectPlayer(clientGuid);
          }
          GameSession newGameSession = CreateNewSession();
          await dbCtx.GameSessions.AddAsync(newGameSession);
          await SaveGameSessionToDbAsync();
          await Clients.Caller.SendAsync("NewSessionCreated", newGameSession.SessionId);
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
  }

  public async Task ClientResetCurrentSession(String sessionId)
  {
    Int32 tryCount = 0;
    while (tryCount < DbRetryCount)
    {
      try
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

  public async Task ClientSearchSession(String sessionId)
    => await Clients.Caller.SendAsync("ServerSessionFindResult", GameSessionExist(sessionId), DateTimeOffset.UtcNow);

  public async Task ClientConnectNew(String sessionId, String clientGuid, String newPlayerName, Boolean restore, Boolean requestFullWords)
  {
    Int32 tryCount = 0;
    while (tryCount < DbRetryCount)
    {
      try
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
            await SaveGameSessionToDbAsync(gameSession);
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
            connectionIdSessionIds.TryAdd(Context.ConnectionId, sessionId);
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

  public async Task ClientConnectAsSpectator(String sessionId)
  {
    GameSession? gameSession = GetGameSession(sessionId);
    if (gameSession is null)
    {
      await SendJoinErrorAsync(ServerJoinError.SessionNotFound);
      return;
    }
    await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    connectionIdSessionIds.TryAdd(Context.ConnectionId, sessionId);
    await SendCurrentAnswerAsync(gameSession, false);
    await SendFullGameStateAsync(gameSession);
  }

  public async Task ClientReconnect(String sessionId, String playerName, Boolean spectatorMode)
  {
    Int32 tryCount = 0;
    while (tryCount < DbRetryCount)
    {
      try
      {
        GameSession? gameSession = GetGameSession(sessionId);
        if (gameSession is null)
        {
          await SendJoinErrorAsync(ServerJoinError.SessionNotFound);
          return;
        }
        if (!spectatorMode) // Check if Spectator
        {
          if (gameSession.ReconnectPlayer(playerName, Context.ConnectionId)) // Actual Player
            await SaveGameSessionToDbAsync(gameSession);
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        connectionIdSessionIds.TryAdd(Context.ConnectionId, sessionId);
        await SendFullGameStateAsync(gameSession);
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

  public async Task ClientSubmitGuess(String playerName, String guess)
  {
    if (!connectionIdSessionIds.ContainsKey(Context.ConnectionId))
      return;
    Int32 tryCount = 0;
    while (tryCount < DbRetryCount)
    {
      String sessionId = connectionIdSessionIds[Context.ConnectionId];
      try
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

  public async Task ClientSubmitGuess2(String sessionId, String playerName, String guess)
  {
    Int32 tryCount = 0;
    while (tryCount < DbRetryCount)
    {
      try
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

  public async Task ClientAdminInfo(String adminKey)
  {
    if (adminKey == Environment.GetEnvironmentVariable("ADMIN_KEY"))
      await Clients.Caller.SendAsync("ServerAdminInfo", await dbCtx.GameSessions!.ToListAsync());
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
    Dictionary<String, PlayerData> strippedDictionary = new();
    foreach (var pair in gameSession.PlayerDataDictionary)
    {
      PlayerData playerData = pair.Value;
      PlayerData newPlayerData = new()
      {
        Index = playerData.Index,
        ConnectionId = "",
        ClientGuid = "",
        DisconnectedDateTime = playerData.DisconnectedDateTime,
        PlayData = playerData.PlayData
      };
      strippedDictionary.Add(pair.Key, newPlayerData);
    }

    if (sendToWholeGroup)
      await Clients.Group(gameSession.SessionId).SendAsync("ServerPlayerData", strippedDictionary);
    else
      await Clients.Caller.SendAsync("ServerPlayerData", strippedDictionary);
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

  private GameSession? GetGameSession(String sessionId) => dbCtx.GameSessions!.Find(sessionId);

  private Boolean GameSessionExist(String sessionId) => GetGameSession(sessionId) is not null;

  public static void RemoveDisconnectedPlayers(Object? sender, System.Timers.ElapsedEventArgs e)
  {
    Task.Run(async () =>
    {
      WordleOffContext tempCtx = new();
      if (tempCtx.GameSessions is not null)
      {
        List<GameSession> gameSessionList = tempCtx.GameSessions.ToList();
        foreach (GameSession gameSession in gameSessionList)
        {
          if (gameSession is not null)
          {
            Int32 tryCount = 0;
            while (tryCount < DbRetryCount)
            {
              String sessionId = gameSession.SessionId;
              try
              {
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
        }
      }
      await tempCtx.DisposeAsync();
    });
  }

  public static void RemoveExpiredSessions(Object? sender, System.Timers.ElapsedEventArgs e)
  {
    Task.Run(async () =>
    {
      WordleOffContext tempCtx = new();
      if (tempCtx.GameSessions is not null)
      {
        Int32 tryCount = 0;
        while (tryCount < DbRetryCount)
        {
          var expiredSessions = tempCtx.GameSessions.ToList().Where(x => x.SessionExpired);
          foreach (GameSession? gameSession in expiredSessions)
            if (gameSession is not null)
              tempCtx.GameSessions.Remove(gameSession);
          try
          {
            await tempCtx.SaveChangesAsync();
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
      await tempCtx.DisposeAsync();
      GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
      GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
    });
  }

  public async override Task OnDisconnectedAsync(Exception? exception)
  {
    connectionIdSessionIds.Remove(Context.ConnectionId, out String? sessionId);
    if (sessionId is not null)
    {
      await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
      Int32 tryCount = 0;
      while (tryCount < DbRetryCount)
      {
        try
        {
          GameSession? gameSession = GetGameSession(sessionId);
          if (gameSession is not null)
          {
            gameSession.DisconnectPlayer(Context.ConnectionId);
            await SaveGameSessionToDbAsync(gameSession);
            await SendFullGameStateAsync(gameSession);
            break;
          }
        }
        catch (DbUpdateConcurrencyException)
        {
          SleepRandomForDbRetry();
          ++tryCount;
        }
        catch (Exception) { }
      }
    }
    latestClients = Clients;
  }

  public static void SleepRandomForDbRetry() => Thread.Sleep(random.Next(1, 150));

  #endregion
}
