using System.ComponentModel.DataAnnotations;

namespace WordleOff.Shared.Games;

public class SessionStat
{
  [Key]
  public String Category { get; set; } = "";
  public UInt64 Count { get; set; } = 0;

  public SessionStat(String category) => Category = category;
}
