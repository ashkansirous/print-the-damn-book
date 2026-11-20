using System.Globalization;

namespace PrintTheDamnBook.Core.Kdp;

/// <summary>
/// The names of the pictures whose crop can be chosen (Slice 36): a story page by its number ("1" and up, the number the check report lists), and the
/// two cover pictures by <see cref="Front"/> and <see cref="Back"/>. The same keys name the files of the crop sheet and the entries of <c>kdp.json</c>.
/// </summary>
public static class KdpPictureKeys
{
    public const string Front = "front";
    public const string Back = "back";

    public static bool IsCover(string? key) => key is Front or Back;

    /// <exception cref="ArgumentOutOfRangeException">The key is not a cover key.</exception>
    public static void EnsureCover(string key)
    {
        if (!IsCover(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, $"A cover picture is \"{Front}\" or \"{Back}\".");
        }
    }

    public static string ForStoryPage(int storyPage) => storyPage.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads the part before the <c>=</c> of an <c>--offset</c>: a story page number from 1 (<paramref name="storyPage"/> set) or a cover key
    /// (<paramref name="coverKey"/> set); false for anything else.
    /// </summary>
    public static bool TryParse(string text, out int storyPage, out string? coverKey)
    {
        storyPage = 0;
        coverKey = null;
        if (IsCover(text))
        {
            coverKey = text;
            return true;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out storyPage) && storyPage >= 1;
    }
}
