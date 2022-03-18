using System.ComponentModel.DataAnnotations;

namespace WordleOff.Server.Hubs;
public class ConnectionIdToSessionId
{
  [Key]
  public String ConnectionId { get; set; } = "";
  public String SessionId { get; set; } = "";

  public ConnectionIdToSessionId(String connectionId, String sessionId)
  {
    ConnectionId = connectionId;
    SessionId = sessionId;
  }
}