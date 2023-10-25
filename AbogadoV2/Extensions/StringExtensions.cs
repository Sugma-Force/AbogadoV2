using System.Text;

namespace AbogadoV2.Extensions;

public class StringExtensions
{
    public static string GenerateRandomString(int length)
    {
        var rnd = new Random();
        const string charset = "1a4567890bcdefuygtruyhijof2";
        var result = new StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            result.Append(charset[rnd.Next(charset.Length)]);
        }

        return result.ToString();
    }
}