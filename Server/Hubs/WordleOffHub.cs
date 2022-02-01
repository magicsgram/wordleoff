using System.Text;
using Microsoft.AspNetCore.SignalR;
using WordleOff.Shared;

namespace WordleOff.Server.Hubs;

public class WordleOffHub : Hub
{
  private static Boolean initialized = false;
  private static Random random = new();
  
  private static Dictionary<String, GameSession> gameSessions = new();
  private static Dictionary<String, String> connectionIdToSessionId = new();

  private static IHubCallerClients? latestClients;

  private static Timer removeDisconnectedPlayersTimer = new(RemoveDisconnectedPlayers, null, 1000, 1000);
  private static Timer removeExpiredSessionsTimer = new(RemoveExpiredSessions, null, 10000, 10000);

  public WordleOffHub() : base()
  {
    if (!initialized)
    {
      // Todo: initialize static objects here.
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
      await Clients.Caller.SendAsync("GameSessionNotFound");
      return;
    }
    gameSessions[sessionId].ResetGame();
    await SendCurrentAnswer(sessionId);
    await SendFullGameState(sessionId);
  }

  public async Task ClientConnectNew(String sessionId, String newPlayerName)
  {
    if (!gameSessions.ContainsKey(sessionId))
    {
      await Clients.Caller.SendAsync("GameSessionNotFound");
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
        await SendJoinError("The name's taken!");
        break;
      case AddPlayerResult.PlayerMaxed:
        await SendJoinError("The session is full. Try again later.");
        break;
      case AddPlayerResult.GameAlreadyStarted:
        await SendJoinError("The session has already begun. Try again later.");
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
      await SendJoinError("The session does not exist.");
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

  public async Task SendJoinError(String message) => await Clients.Caller.SendAsync("ServerJoinFail", message);

  #endregion


  #region Other Server Codes

  public static String CreateNewSession()
  {
    String newSessionId = "";
    do
    {
      List<String> segments = new();
      for (Int32 i = 0; i < 3; ++i)
        segments.Add(random.Next(100, 999).ToString("000"));
      newSessionId = String.Join("-", segments);
    } while (gameSessions.ContainsKey(newSessionId));
    gameSessions.Add(newSessionId, new GameSession(newSessionId));
    
    // newSessionId = "111-111-111";
    // gameSessions.Add(newSessionId, new GameSession(newSessionId, "mount"));
    return newSessionId;
  }

  public static void RemoveDisconnectedPlayers(Object? state)
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

  public static void RemoveExpiredSessions(Object? state)
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
