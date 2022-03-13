using System.Text;

namespace WordleOff.Shared.Games;

public class EncryptDecrypt
{
  private static Random random = new();

  // Very simple encryption/decryption to prevent very obvious browser inspector cheating
  public static String XorEncrypt(String txt)
  {
    StringBuilder sb = new(txt.Length * 2);
    foreach (Char c in txt)
    {
      sb.Append(c);
      sb.Append((Char)('a' + random.Next(0, 26)));
    }
    String encrypted = XorEncryptDecrypt(sb.ToString());
    return encrypted;
  }

  // Very simple encryption/decryption to prevent very obvious browser inspector cheating
  public static String XorDecrypt(String txt)
  {
    StringBuilder sb = new(txt.Length / 2);
    for (Int32 i = 0; i < txt.Length; i += 2)
      sb.Append(txt[i]);
    String decrypted = XorEncryptDecrypt(sb.ToString());
    return decrypted;
  }

  // Xor processing
  private static String XorEncryptDecrypt(String txt)
  {
    StringBuilder sb = new(txt.Length);
    foreach (Char c in txt)
    {
      Char processed = (Char)(c ^ 179);
      sb.Append(processed);
    }
    return sb.ToString();
  }
}
