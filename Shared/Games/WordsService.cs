using System.IO.Compression;
using System.Text;

namespace WordleOff.Shared.Games;

public class WordsService
{
  private static Boolean initialized = false;
  private static Random random = new();
  private static List<String> answersList = new();
  public static HashSet<String> FullWordsSet = new();
  public static Byte[] CompressedFullWordsBytes
  {
    get
    {
      if (!initialized)
        Initialize();
      return compressedFullWordsBytes;
    }
  }
  private static Byte[] compressedFullWordsBytes = new Byte[0];
  

  public static String NextRandomAnswer()
  {
    if (!initialized)
      Initialize();
    Int32 randomIndex = random.Next(answersList.Count - 1);
    return answersList[randomIndex];
  }
  
  public static void Initialize()
  {
    // Load Full Words
    DirectoryInfo fullWordsDirectoryInfo = Directory.GetParent(".") ?? throw new Exception("Parent Directory Not Found");
    String fullWordsPath = Path.Combine(fullWordsDirectoryInfo.FullName, "words-all.txt");
    StreamReader fullWordsStreamReader = new(fullWordsPath);
    List<String> fullWordsTempList = new();
    while (!fullWordsStreamReader.EndOfStream)
    {
      String word = fullWordsStreamReader.ReadLine() ?? throw new Exception("Words not found");
      word = word.ToLower().Trim();
      if (word.Length > 0)
          fullWordsTempList.Add(word);          
    }
    fullWordsStreamReader.Close();
    
    // Load answers
    DirectoryInfo answersDirectoryInfo = Directory.GetParent(".") ?? throw new Exception("Parent Directory Not Found");
    String answersPath = Path.Combine(answersDirectoryInfo.FullName, "words-wordle.txt");
    StreamReader answersStreamReader = new(answersPath);
    while (!answersStreamReader.EndOfStream)
    {
      String word = answersStreamReader.ReadLine() ?? throw new Exception("Words not found");
      word = word.ToLower().Trim();
      if (word.Length > 0)
      {
        answersList.Add(word);
        fullWordsTempList.Add(word);
      } 
    }
    answersStreamReader.Close();
    Random random = new();
    answersList = answersList.Distinct().ToList();
    FullWordsSet = fullWordsTempList.Distinct().ToHashSet();

    // Create compressed full words list. (Optimizing for transfer size)
    MemoryStream output = new MemoryStream();
    using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.SmallestSize))
    {
      String fullString = String.Join("\n", FullWordsSet);
      Byte[] fullStringBytes = UTF8Encoding.UTF8.GetBytes(fullString);
      dstream.Write(fullStringBytes, 0, fullStringBytes.Length);
    }
    compressedFullWordsBytes = output.ToArray();

    initialized = true;
  }
}
