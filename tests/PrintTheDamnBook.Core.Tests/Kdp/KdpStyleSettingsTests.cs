using PrintTheDamnBook.Core.Kdp;

namespace PrintTheDamnBook.Core.Tests.Kdp;

/// <summary><c>kdp.json</c> <c>styles</c> (Slice 46 decision 9): speaker colours, main character and title picture; older files still read; a bad file is rejected, never repaired.</summary>
public class KdpStyleSettingsTests
{
    [Fact]
    public void A_file_without_styles_reads_with_none_and_writes_none_back()
    {
        var settings = KdpBookSettings.Parse("""{ "pages": { "7": { "cropOffsetPx": 60 } } }""");

        Assert.Empty(settings.Styles.SpeakerColours);
        Assert.Null(settings.Styles.MainCharacter);
        Assert.DoesNotContain("styles", settings.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Styles_round_trip_through_the_file()
    {
        var settings = KdpBookSettings.Empty
            .WithSpeaker("Seagull", "#1f4fa0")
            .WithSpeaker("Pancake Polly", "#2F6B34")
            .WithMainCharacter("Seagull", "She speaks on most pages")
            .WithTitlePicture(new KdpTitlePicture(3, 0.5, 0.4, 0.6));

        var read = KdpBookSettings.Parse(settings.ToJson());

        Assert.Equal("#1F4FA0", read.Styles.SpeakerColours["Seagull"]);
        Assert.Equal("#2F6B34", read.Styles.SpeakerColours["Pancake Polly"]);
        Assert.Equal(new KdpMainCharacter("Seagull", "She speaks on most pages"), read.Styles.MainCharacter);
        Assert.Equal(new KdpTitlePicture(3, 0.5, 0.4, 0.6), read.Styles.TitlePicture);
        Assert.Contains("\"styles\"", read.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_written_file_holds_only_the_three_style_keys_and_no_computed_property()
    {
        var settings = KdpBookSettings.Empty
            .WithSpeaker("Seagull", "#1F4FA0")
            .WithMainCharacter("Seagull", "She speaks on most pages")
            .WithTitlePicture(new KdpTitlePicture(3, 0.5, 0.4, 0.6))
            .WithCoverText("front", new KdpCoverText(new KdpTextBox(0.1, 0.1, 0.5, 0.2), "#FFFFFF", "#000000"));
        var json = settings.ToJson();

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var styles = doc.RootElement.GetProperty("styles").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
        var front = doc.RootElement.GetProperty("covers").GetProperty("front").GetProperty("text").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);

        Assert.Equal(["mainCharacter", "speakers", "titlePicture"], styles);
        Assert.Equal(["box", "color", "shadow"], front);
        Assert.DoesNotContain("SpeakerColours", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"colors\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"isValid\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_file_with_the_old_extra_SpeakerColours_key_still_reads()
    {
        var settings = KdpBookSettings.Parse("""{ "styles": { "speakers": { "Seagull": "#1F4FA0" }, "SpeakerColours": { "Seagull": "#1F4FA0" } } }""");

        Assert.Equal("#1F4FA0", settings.Styles.SpeakerColours["Seagull"]);
        Assert.DoesNotContain("SpeakerColours", settings.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Styles_are_kept_when_the_offsets_and_the_book_identity_change()
    {
        var settings = KdpBookSettings.Empty.WithSpeaker("Seagull", "#1F4FA0").WithOffset(2, 40).ForBook("id1", "A Book").WithCoverOffset("front", 10);

        Assert.Equal("#1F4FA0", settings.Styles.SpeakerColours["Seagull"]);
        Assert.Equal("#1F4FA0", KdpBookSettings.Parse(settings.ToJson()).Styles.SpeakerColours["Seagull"]);
    }

    [Fact]
    public void Clearing_the_last_choice_removes_the_styles_entry()
    {
        var settings = KdpBookSettings.Empty.WithSpeaker("Seagull", "#1F4FA0").WithMainCharacter("Seagull").WithSpeaker("Seagull", null).WithMainCharacter(null);

        Assert.DoesNotContain("styles", settings.ToJson(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "styles": { "speakers": { "Seagull": "blue" } } }""")]
    [InlineData("""{ "styles": { "speakers": { "": "#1F4FA0" } } }""")]
    [InlineData("""{ "styles": { "mainCharacter": { "name": "" } } }""")]
    [InlineData("""{ "styles": { "titlePicture": { "page": 0, "centerX": 0.5, "centerY": 0.5, "diameter": 0.5 } } }""")]
    [InlineData("""{ "styles": { "titlePicture": { "page": 1, "centerX": 1.5, "centerY": 0.5, "diameter": 0.5 } } }""")]
    [InlineData("""{ "styles": { "titlePicture": { "page": 1, "centerX": 0.5, "centerY": 0.5, "diameter": 0 } } }""")]
    public void A_bad_styles_entry_is_rejected_so_the_file_is_never_overwritten(string json)
    {
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Parse(json));
    }

    [Fact]
    public void A_title_picture_outside_the_picture_or_a_colour_that_is_not_one_is_refused_when_set()
    {
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Empty.WithTitlePicture(new KdpTitlePicture(1, 0.5, 0.5, 0)));
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Empty.WithSpeaker("Seagull", "blue"));
        Assert.Throws<KdpSettingsException>(() => KdpBookSettings.Empty.WithSpeaker(" ", "#1F4FA0"));
    }
}
