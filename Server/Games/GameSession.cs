using WordleOff.Shared;

namespace WordleOff.Server.Hubs;

public enum AddPlayerResult
{
  Success,
  PlayerNameExist,
  PlayerMaxed,
  GameAlreadyStarted,
  Unknown
}

public enum EnterWordResult
{
  Success,
  PlayerNotFound
}

public class GameSession
{
  private static Random random = new();

  public const Int32 MaxPlayers = 4;
  public const Int32 GameSessionExpireMinutes = 5;
  public const Int32 ConnectionExpireSeconds = 5;

  private DateTime? noPlayerSince = DateTime.Now;

  public String SessionId { get; set; } = "";
  public String CurrentAnswer { get; set; } = "";
  public Dictionary<String, PlayerData> PlayerDataDictionary { get; set; } = new();

  public GameSession(String sessionId, String newAnswer)
  {
    SessionId = sessionId;
    CurrentAnswer = newAnswer;
  }

  public void ResetGame(String newAnswer)
  {
    CurrentAnswer = newAnswer;
    foreach (var pair in PlayerDataDictionary)
      pair.Value.PlayData.Clear();
  }

  public AddPlayerResult AddPlayer(String connectionId)
  {
    String newPlayerName = GetRandomPlayerName();
    if (PlayerDataDictionary.Count == MaxPlayers)
      return AddPlayerResult.PlayerMaxed;

    if (PlayerDataDictionary.ContainsKey(newPlayerName))
      return AddPlayerResult.PlayerNameExist;
    
    if (PlayerDataDictionary.Any(pair => pair.Value.PlayData.Count > 0))
      return AddPlayerResult.GameAlreadyStarted;

    Int32 maxIndex = PlayerDataDictionary.Count == 0 ? 0 : PlayerDataDictionary.Values.Max(x => x.Index);

    PlayerDataDictionary.Add(
      newPlayerName,
      new PlayerData() {
        Index = maxIndex + 1,
        ConnectionId = connectionId,
        PlayData = new(),
        DisconnectedDateTime = null
      }
    );
    noPlayerSince = null;
    return AddPlayerResult.Success;
  }

  public void ReconnectPlayer(String playerName, String newConnectionId)
  {
    if (!PlayerDataDictionary.ContainsKey(playerName))
      return;
    PlayerDataDictionary[playerName].ConnectionId = newConnectionId;
    PlayerDataDictionary[playerName].DisconnectedDateTime = null;
  }

  public void DisconnectPlayer(String connectionId)
  {
    var pairs = PlayerDataDictionary.Where(pair => pair.Value.ConnectionId == connectionId);
    if (pairs.Count() > 0)
    {
      var pair = pairs.First();
      pair.Value.DisconnectedDateTime = DateTime.Now;
    }
  }

  public Boolean RemoveDisconnectedPlayer()
  {
    DateTime now = DateTime.Now;
    
    var playerNamesToRemove = PlayerDataDictionary
      .Where((pair) => {
        TimeSpan disconnectedTimeSpan = now - (pair.Value.DisconnectedDateTime ?? now);
        return TimeSpan.FromSeconds(ConnectionExpireSeconds) < disconnectedTimeSpan;
      })
      .Select((pair) => pair.Key).ToList();

    foreach(String playerName in playerNamesToRemove)
      PlayerDataDictionary.Remove(playerName);
    return playerNamesToRemove.Count > 0;
  }
  
  public Boolean SessionExpired()
  {
    DateTime now = DateTime.Now;
    TimeSpan noPlayerTimeSpan = now - (noPlayerSince ?? now);
    return TimeSpan.FromMinutes(GameSessionExpireMinutes) < noPlayerTimeSpan;
  }

  public String GetRandomPlayerName()
  {
    String newPlayerName = "";
    do 
    {
      newPlayerName = $"Player#{random.Next(100, 999).ToString("000")}";
    } while(PlayerDataDictionary.ContainsKey(newPlayerName));
    return newPlayerName;
  }

  public EnterWordResult EnterGuess(String playerName, String word)
  {
    if (!PlayerDataDictionary.ContainsKey(playerName))
      return EnterWordResult.PlayerNotFound;

    PlayerDataDictionary[playerName].PlayData.Add(word);
    return EnterWordResult.Success;
  }
}
