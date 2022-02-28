using WordleOff.Shared.Games;

namespace WordleOff.Client.Shared
{
  public class ServerJoinErrorMessages
  {
    private static readonly Lazy<ServerJoinErrorMessages> singleTon = new(() => new ServerJoinErrorMessages());

    private readonly Dictionary<ServerJoinError, String> messages;
    private ServerJoinErrorMessages()
    {
      messages = new();
      messages.Add(ServerJoinError.SessionNotFound, "The session does not exist.");
      messages.Add(ServerJoinError.NameTaken, "The name's taken!");
      messages.Add(ServerJoinError.SessionFull, "The session is full. Try again later.");
      messages.Add(ServerJoinError.SessionInProgress, "The session has already begun. Try again later.");
      messages.Add(ServerJoinError.CannotRestore, "Could not restore previous connection. You can still join again.");
    }
    
    public static Dictionary<ServerJoinError, String> Maps => singleTon.Value.messages;
  }
}
