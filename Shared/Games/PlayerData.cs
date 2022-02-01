namespace WordleOff.Shared;

public class PlayerData
{
    public Int32 Index { get; set; }
    public String ConnectionId { get; set; } = "";
    public List<String> PlayData { get; set; } = new();
    public DateTime? DisconnectedDateTime { get; set; }

    public Boolean AnswerGuessCorrectly(String answer) => PlayData.Count > 0 && PlayData.Last() == answer;
}
