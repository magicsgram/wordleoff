using Microsoft.AspNetCore.SignalR;
using WordleOff.Shared.Games;

namespace WordleOff.Server.Hubs;

public class WordleOffHub : Hub
{
  private static Boolean initialized = false;
  private static readonly Random random = new();

  private static readonly Dictionary<String, GameSession> gameSessions = new();
  private static readonly Dictionary<String, String> connectionIdToSessionId = new();

  private static IHubCallerClients? latestClients;

  private static System.Timers.Timer? removeDisconnectedPlayersTimer;
  private static System.Timers.Timer? removeExpiredSessionsTimer;

  public WordleOffHub() : base()
  {
    if (!initialized)
    {
      removeDisconnectedPlayersTimer = new(1000);
      removeDisconnectedPlayersTimer.Elapsed += RemoveDisconnectedPlayers;
      removeDisconnectedPlayersTimer.AutoReset = true;
      removeDisconnectedPlayersTimer.Enabled = true;
      removeDisconnectedPlayersTimer.Start();

      removeExpiredSessionsTimer = new(15000);
      removeExpiredSessionsTimer.Elapsed += RemoveExpiredSessions;
      removeExpiredSessionsTimer.AutoReset = true;
      removeExpiredSessionsTimer.Enabled = true;
      removeExpiredSessionsTimer.Start();

      initialized = true;
    }
  }

  #region Received from Client

  public async Task ClientCreateNewSession()
  {
    if (connectionIdToSessionId.ContainsKey(Context.ConnectionId))
    {
      gameSessions[Context.ConnectionId].DisconnectPlayer(Context.ConnectionId);
      connectionIdToSessionId.Remove(Context.ConnectionId);
    }

    String newSessionId = CreateNewSession();
    await Clients.Caller.SendAsync("NewSessionCreated", newSessionId);
  }

  public async Task ClientResetCurrentSession(String sessionId)
  {
    if (!gameSessions.ContainsKey(sessionId))
    {
      await SendJoinError(ServerJoinError.SessionNotFound);
      return;
    }
    gameSessions[sessionId].ResetGame();
    await SendCurrentAnswer(sessionId);
    await SendFullGameState(sessionId);
  }

  public async Task ClientSearchSession(String sessionId) => await Clients.Caller.SendAsync("ServerSessionFindResult", gameSessions.ContainsKey(sessionId));

  public async Task ClientConnectNew(String sessionId, String newPlayerName)
  {
    if (!gameSessions.ContainsKey(sessionId))
    {
      await SendJoinError(ServerJoinError.SessionNotFound);
      return;
    }

    var result = gameSessions[sessionId].AddPlayer(Context.ConnectionId, newPlayerName);
    switch (result)
    {
      case AddPlayerResult.Success:
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        connectionIdToSessionId.Add(Context.ConnectionId, sessionId);
        await SendFullWordsCompressed();
        await SendCurrentAnswer(sessionId, false);
        await SendFullGameState(sessionId);
        break;
      case AddPlayerResult.PlayerNameExist:
        await SendJoinError(ServerJoinError.NameTaken);
        break;
      case AddPlayerResult.PlayerMaxed:
        await SendJoinError(ServerJoinError.SessionFull);
        break;
      case AddPlayerResult.GameAlreadyStarted:
        await SendJoinError(ServerJoinError.SessionInProgress);
        break;
      default:
        // TODO: Send an error message
        break;
    }
  }

  public async Task ClientReconnect(String sessionId, String playerName)
  {
    if (!gameSessions.ContainsKey(sessionId))
    {
      await SendJoinError(ServerJoinError.SessionNotFound);
      return;
    }
    await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    connectionIdToSessionId.Add(Context.ConnectionId, sessionId);
    gameSessions[sessionId].ReconnectPlayer(playerName, Context.ConnectionId);
  }

  public async Task ClientUpdatePlayerName(String newPlayerName)
  {
    String connectionId = Context.ConnectionId;
    if (!connectionIdToSessionId.ContainsKey(connectionId))
      return;
    String sessionId = connectionIdToSessionId[connectionId];
    var pair = gameSessions[sessionId].PlayerDataDictionary.Where(x => x.Value.ConnectionId == connectionId).First();
    String oldPlayerName = pair.Key;
    PlayerData oldPlayerData = pair.Value;
    gameSessions[sessionId].PlayerDataDictionary.Remove(oldPlayerName);
    gameSessions[sessionId].PlayerDataDictionary.Add(newPlayerName, oldPlayerData);
    await SendFullGameState(sessionId);
  }

  public async Task ClientSubmitGuess(String playerName, String guess)
  {
    String connectionId = Context.ConnectionId;
    if (!connectionIdToSessionId.ContainsKey(connectionId))
      return;
    String sessionId = connectionIdToSessionId[connectionId];
    gameSessions[sessionId].EnterGuess(playerName, guess);
    await SendFullGameState(sessionId);
  }

  #endregion


  #region Send to Client

  public async Task SendFullWordsCompressed() => await Clients.Caller.SendAsync("ServerFullWordsCompressed", WordsService.CompressedFullWordsBytes);

  public async Task SendCurrentAnswer(String sessionId, Boolean sendToWholeGroup = true)
  {
    if (sendToWholeGroup)
      await Clients.Group(sessionId).SendAsync("ServerCurrentAnswer", gameSessions[sessionId].CurrentAnswer);
    else
      await Clients.Caller.SendAsync("ServerCurrentAnswer", gameSessions[sessionId].CurrentAnswer);
  }

  public async Task SendFullGameState(String sessionId, Boolean sendToWholeGroup = true)
  {
    if (sendToWholeGroup)
      await Clients.Group(sessionId).SendAsync("ServerPlayerData", gameSessions[sessionId].PlayerDataDictionary);
    else
      await Clients.Caller.SendAsync("ServerPlayerData", gameSessions[sessionId].PlayerDataDictionary);
  }

  public async Task SendJoinError(ServerJoinError error) => await Clients.Caller.SendAsync("ServerJoinError", error);

  #endregion


  #region Other Server Codes

  public String CreateNewSession()
  {
    String newSessionId = GetNewGameSessionId();
    gameSessions.Add(newSessionId, new GameSession(newSessionId));

    // //For Testing Only
    // String newSessionId = "123-123-123";
    // gameSessions.Add(newSessionId, new GameSession(newSessionId, "mount"));
    return newSessionId;
  }

  private static String GetNewGameSessionId()
  {
    String newSessionId;
    do
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
    } while (gameSessions.ContainsKey(newSessionId));
    return newSessionId;
  }

  public static void RemoveDisconnectedPlayers(Object? sender, System.Timers.ElapsedEventArgs e)
  {
    Task.Run(async () =>
    {
      foreach (var gameSession in gameSessions.Values)
        if (gameSession.RemoveDisconnectedPlayer())
          if (latestClients is not null)
          {
            WordleOffHub newHub = new();
            newHub.Clients = latestClients;
            await newHub.SendFullGameState(gameSession.SessionId);
          }
    });
  }

  public static void RemoveExpiredSessions(Object? sender, System.Timers.ElapsedEventArgs e)
  {
    var expiredSessions = gameSessions.Where(x => x.Value.SessionExpired);
    foreach (var pair in expiredSessions)
      gameSessions.Remove(pair.Key);
  }

  public async override Task OnDisconnectedAsync(Exception? exception)
  {
    if (!connectionIdToSessionId.ContainsKey(Context.ConnectionId))
      return;

    String sessionId = connectionIdToSessionId[Context.ConnectionId];
    gameSessions[sessionId].DisconnectPlayer(Context.ConnectionId);
    connectionIdToSessionId.Remove(Context.ConnectionId);
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
    latestClients = Clients;
  }

  #endregion
}
