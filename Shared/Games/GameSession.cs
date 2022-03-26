using System.ComponentModel.DataAnnotations;

namespace WordleOff.Shared.Games;

public enum AddPlayerResult
{
  Success,
  ConnectionRestored,
  PlayerNameExist,
  PlayerMaxed,
  CannotRestore,
  Unknown
}

public class GameSession
{
  public const Int32 MaxPlayers = 16;
  private const Int32 GameSessionExpireMinutes = 120;
  private const Int32 PastAnswersMaxSize = 500;

  [Key]
  public String SessionId { get; set; } = "";
  public Dictionary<String, PlayerData> PlayerDataDictionary { get; set; } = new();
  public Queue<String> PastAnswers { get; set; } = new();
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
  public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

  public DateTimeOffset GameStartedAt { get; set; } = DateTimeOffset.UtcNow;
  public Int32 TotalGameTimeSeconds { get; set; } = 0;
  public Int32 TotalGamesPlayed { get; set; } = 0;
  public Int32 TotalPlayersConnected { get; set; } = 0;
  public Int32 MaxPlayersConnected { get; set; } = 0;

  public String CurrentAnswer { get { return PastAnswers.Last(); } }
  public Boolean SessionExpired
  {
    get
    {
      DateTimeOffset now = DateTimeOffset.UtcNow;
      TimeSpan sinceLastUpdate = now - UpdatedAt;
      return TimeSpan.FromMinutes(GameSessionExpireMinutes) < sinceLastUpdate;
    }
  }

  public GameSession() { } // Never Called

  public GameSession(String sessionId, String? answer = null)
  {
    SessionId = sessionId;
    UpdatedAt = CreatedAt = DateTimeOffset.UtcNow;
    if (answer is null)
      SetNewRandomAnswer();
    else
      PastAnswers.Enqueue(answer);
  }

  public void ResetGame()
  {
    SetNewRandomAnswer();

    foreach (var pair in PlayerDataDictionary)
      pair.Value.PlayData.Clear();
    if (GameStartedAt != DateTimeOffset.MaxValue)
      TotalGameTimeSeconds += (Int32)Math.Round((UpdatedAt - GameStartedAt).TotalSeconds);

    DateTimeOffset now = DateTimeOffset.UtcNow;
    GameStartedAt = DateTimeOffset.MinValue;
    UpdatedAt = now;
  }

  private void SetNewRandomAnswer()
  {
    String newAnswer;
    do
    {
      newAnswer = WordsService.NextRandomAnswer();
    } while (PastAnswers.Contains(newAnswer));
    PastAnswers.Enqueue(newAnswer);
    while (PastAnswers.Count > PastAnswersMaxSize)
      PastAnswers.Dequeue();
  }

  public AddPlayerResult AddPlayer(String connectionId, String clientGuid, String newPlayerName, Boolean restore)
  {
    if (PlayerDataDictionary.ContainsKey(newPlayerName))
    {
      if (PlayerDataDictionary[newPlayerName].ClientGuid == clientGuid)
      { // Restoring connection
        PlayerDataDictionary[newPlayerName].ConnectionId = connectionId;
        PlayerDataDictionary[newPlayerName].DisconnectedDateTime = null;
        UpdatedAt = DateTimeOffset.UtcNow;
        return AddPlayerResult.ConnectionRestored;
      }
      else
        return AddPlayerResult.PlayerNameExist;
    }
    else if (restore)
      return AddPlayerResult.CannotRestore;

    if (PlayerDataDictionary.Count == MaxPlayers)
      return AddPlayerResult.PlayerMaxed;

    //Boolean midJoin = false;
    //if (PlayerDataDictionary.Any(pair => pair.Value.PlayData.Count > 0))
    //  midJoin = true;

    Int32 maxIndex = PlayerDataDictionary.Count == 0 ? 0 : PlayerDataDictionary.Values.Max(x => x.Index);

    PlayerData newPlayerData = new()
    {
      Index = maxIndex + 1,
      ConnectionId = connectionId,
      ClientGuid = clientGuid,
      PlayData = new(),
      DisconnectedDateTime = null
    };
    DateTimeOffset now = DateTimeOffset.UtcNow;
    if (PlayerDataDictionary.Count == 0)
      GameStartedAt = DateTimeOffset.MaxValue;

    PlayerDataDictionary.Add(newPlayerName, newPlayerData);
    UpdatedAt = now;
    ++TotalPlayersConnected;
    if (MaxPlayersConnected < PlayerDataDictionary.Count)
      MaxPlayersConnected = PlayerDataDictionary.Count;
    return AddPlayerResult.Success;
  }

  public Boolean ReconnectPlayer(String playerName, String newConnectionId)
  {
    if (!PlayerDataDictionary.ContainsKey(playerName))
      return false;
    PlayerDataDictionary[playerName].ConnectionId = newConnectionId;
    PlayerDataDictionary[playerName].DisconnectedDateTime = null;
    UpdatedAt = DateTimeOffset.UtcNow;
    return true;
  }

  public void DisconnectPlayer(String connectionId)
  {
    DateTimeOffset now = DateTimeOffset.UtcNow;
    var pairs = PlayerDataDictionary.Where(pair => pair.Value.ConnectionId == connectionId);
    if (pairs.Any())
    {
      var pair = pairs.First();
      pair.Value.DisconnectedDateTime = now;
      UpdatedAt = now;
    }
  }

  public void TreatAllPlayersAsDisconnected(out Boolean updated)
  { // This is useful when the server restarts and everyone needs to connect again
    DateTimeOffset now = DateTimeOffset.UtcNow;
    DateTimeOffset oneMinuteFromNow = now + TimeSpan.FromSeconds(60); // Give extra time for people to reconnect
    updated = false;
    foreach (var pair in PlayerDataDictionary)
      if (pair.Value.DisconnectedDateTime is null)
      {
        pair.Value.DisconnectedDateTime = oneMinuteFromNow;
        updated = true;
        UpdatedAt = now;
      }
  }

  public Boolean RemoveDisconnectedPlayer()
  {
    DateTimeOffset now = DateTimeOffset.UtcNow;
    var playerNamesToRemove = PlayerDataDictionary
      .Where((pair) => {
        TimeSpan disconnectedTimeSpan = now - (pair.Value.DisconnectedDateTime ?? now);
        return TimeSpan.FromSeconds(CommonValues.ConnectionExpireSeconds) < disconnectedTimeSpan;
      })
      .Select((pair) => pair.Key).ToList();
    foreach (String playerName in playerNamesToRemove)
      PlayerDataDictionary.Remove(playerName);
    if (PlayerDataDictionary.Count == 0 && playerNamesToRemove.Count > 0)
    {
      SetNewRandomAnswer();
      if (GameStartedAt != DateTimeOffset.MaxValue)
        TotalGameTimeSeconds += (Int32)Math.Round((UpdatedAt - GameStartedAt).TotalSeconds);
      GameStartedAt = DateTimeOffset.MaxValue;
      UpdatedAt = now;
    }
    return playerNamesToRemove.Count > 0;
  }

  public void PrepForRemoval()
  { // There are still players connected. They'll get booted.
    if (PlayerDataDictionary.Count > 0)
      if (GameStartedAt != DateTimeOffset.MaxValue)
        TotalGameTimeSeconds += (Int32)Math.Round((UpdatedAt - GameStartedAt).TotalSeconds);
  }

  public Int32 EnterGuess(String playerName, String word)
  {
    var now = DateTimeOffset.UtcNow;
    UpdatedAt = now;

    if (!PlayerDataDictionary.ContainsKey(playerName))
      return 0;

    if (PlayerDataDictionary[playerName].PlayData.Count >= 6)
      return 0;
    if (PlayerDataDictionary.Count(x => x.Value.PlayData.Count > 0) == 0) // First play of the game
    {
      GameStartedAt = now;
      ++TotalGamesPlayed;
    }
    PlayerDataDictionary[playerName].PlayData.Add(word);
    PlayerDataDictionary[playerName].DisconnectedDateTime = null;
    return PlayerDataDictionary[playerName].PlayData.Count;
  }
}
