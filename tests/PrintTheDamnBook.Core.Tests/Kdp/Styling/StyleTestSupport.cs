using System.Text.RegularExpressions;
using PrintTheDamnBook.Core.Kdp.Styling;
using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Tests.Kdp.Styling;

/// <summary>Helpers and real excerpts of the five catalog books of Slice 46 decision 14, used by every styling test.</summary>
internal static class StyleTestSupport
{
    public static readonly Rgb White = new(0xFF, 0xFF, 0xFF);

    // X Marks the Treasure!
    public const string XMarksPage1 = "Seagull flew high, high up over the sea. Down below she could see little boats, a sandy beach and a sunny harbour.\n\nThen she spotted a big pirate hat she knew. It was Captain Kathryn! She was running to her ship, waving a piece of paper. She looked so happy!";
    public const string XMarksSquawk = "Down, down, down swooped Seagull, and she landed right on top of Kathryn's big hat.\n\n\"Squawk! Squawk! Squawk! What's up, Kathryn?\" said Seagull.\n\nKathryn held up an old map. It had a little island on it and a big red X. \"Look! I've found the map to the lost treasure!\" said Kathryn. \"X marks the treasure!\"";
    public const string XMarksLong = "The trip was long, long, long. After a while, everyone on deck felt slow and grumbly, and nobody knew why.";
    public const string XMarksSoftly = "\"Squawk, squawk, squawk,\" said Seagull softly, and she snuggled down on Kathryn's big hat.";
    public const string XMarksBlueBowl = "And there was Pancake Polly, stirring pancake mix in her big blue bowl.";

    // The Gingerbread Man
    public const string GingerRun = "\"Run, run, as fast as you can! You can't catch me, I'm the Gingerbread Man!\" said Gingerbread Man.";
    public const string GingerQuote = "\"Run, run, as fast as you can! You can't catch me, I'm the Gingerbread Man!\"";
    public const string GingerStop = "\"Stop, stop!\" said Granny. \"Come back and give me a big cuddle!\"";
    public const string GingerPitter = "The Gingerbread Man hopped off the tray and ran across the kitchen floor. Pitter-pat, pitter-pat, pitter-pat! Running was such fun!";

    // The Three Little Pigs
    public const string PigsNames = "Once upon a time, three little pigs left home to build houses of their very own. Their names were Pip, Pop and Pim.";
    public const string PigsYay = "Pip was in a big hurry. He built his house out of straw. \"Yay! Now I can play all day!\" said Pip.";
    public const string PigsSlowly = "Pim worked slowly and carefully. He built his house out of bricks, one by one.";
    public const string PigsKnock = "He knocked on the door. Knock, knock, knock!\n\n\"No, no, no! Not by the hair of my chinny chin chin!\" said Pip.";

    // Little Red Riding Hood
    public const string RedHood = "Once upon a time, a little girl lived with her mum at the edge of a big green wood. Her grandma had made her a bright red hood, and she wore it every single day.";
    public const string RedHaHa = "Ha, ha, ha! Hee, hee, hee! Everybody laughed.";
    public const string RedBigEyes = "\"Wolf, what big eyes you have!\" said Red Riding Hood.\n\n\"All the better to see you with!\" said Wolf.";

    // The Ugly Duckling
    public const string DuckGreatBig = "Out came a big grey duckling with a long neck and great big flat feet.\n\n\"Oh my, what a great big baby!\" said Mother Duck.";
    public const string DuckTinyBit = "But the biggest egg didn't crack at all. Not even a tiny bit!";
    public const string DuckYellow = "Out popped three fluffy yellow ducklings.";
    public const string DuckSplash = "Splish-splash, splish-splash, splish-splash! And guess who swam the fastest?";

    public const string FarsiTriplet = "مرغ دریایی اومد پایین پایین پایین و نشست درست رو کلاه گنده‌ی کاترین.";
    public const string FarsiSquawk = "قاق قاق قاق! چی شده کاترین؟";
    public const string ArabicSquawk = "قاق! قاق! قاق! ماذا هناك يا كاترين؟";

    public static StyledPage Style(string text, string language = "en", Rgb? tint = null)
    {
        return new StoryStyler([text], language).Style(0, tint);
    }

    public static IReadOnlyList<StyledRun> Runs(StyledPage page) => page.Paragraphs.SelectMany(p => p.Runs).ToList();

    /// <summary>The look of the run that holds <paramref name="word"/> as a whole word (its first occurrence).</summary>
    public static RunLook LookOf(StyledPage page, string word)
    {
        var pattern = new Regex($@"(?<![\p{{L}}'-]){Regex.Escape(word)}(?![\p{{L}}'-])", RegexOptions.CultureInvariant);
        return Runs(page).First(r => pattern.IsMatch(r.Text)).Look;
    }

    public static bool IsPlain(RunLook look) => look == RunLook.Body;
}
