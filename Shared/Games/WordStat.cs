using System.ComponentModel.DataAnnotations;

namespace WordleOff.Shared.Games;
public class WordStat
{
  [Key]
  public String Word { get; set; } = "";
  [Timestamp]
  public UInt32 Version { get; set; }
  public UInt64 SubmitCountTotal { get; set; } = 0;
  public UInt64 SubmitCountRound1 { get; set; } = 0;
  public UInt64 SubmitCountRound2 { get; set; } = 0;
  public UInt64 SubmitCountRound3 { get; set; } = 0;
  public UInt64 SubmitCountRound4 { get; set; } = 0;
  public UInt64 SubmitCountRound5 { get; set; } = 0;
  public UInt64 SubmitCountRound6 { get; set; } = 0;

  public WordStat(String word) => Word = word;

  public void WordSubmitted(Int32 round)
  {
    ++SubmitCountTotal;
    switch (round)
    {
      case 1:
        ++SubmitCountRound1;
        break;
      case 2:
        ++SubmitCountRound2;
        break;
      case 3:
        ++SubmitCountRound3;
        break;
      case 4:
        ++SubmitCountRound4;
        break;
      case 5:
        ++SubmitCountRound5;
        break;
      case 6:
        ++SubmitCountRound6;
        break;
    }
  }
}