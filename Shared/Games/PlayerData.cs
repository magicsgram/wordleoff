namespace WordleOff.Shared.Games;

public class PlayerData
{
  public Int32 Index { get; set; }
  public String ClientGuid { get; set; } = "";
  public String ConnectionId { get; set; } = "";
  public List<String> PlayData { get; set; } = new();
  public DateTimeOffset? DisconnectedDateTime { get; set; }

  public Boolean AnswerGuessedCorrectly(String answer) => PlayData.Count > 0 && PlayData.Last() == answer;
}
