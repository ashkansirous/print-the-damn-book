using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Core.Kdp.Styling;

namespace PrintTheDamnBook.Kdp.Styling;

/// <summary>A <c>--main-character</c> request: the cast name and the style agent's reason, or a null name to clear the choice.</summary>
public sealed record KdpMainCharacterChange(string? Name, string? Reason = null);

/// <summary>A <c>--title-picture</c> request: the picture and circle to save, or null to clear the choice.</summary>
public sealed record KdpTitlePictureChange(KdpTitlePicture? Picture);

/// <summary>
/// The style choices of one run (<c>--speaker</c>, <c>--main-character</c>, <c>--title-picture</c>, Slice 46 decision 9), set only through the command line.
/// <see cref="Speakers"/> maps a cast name to its colour, or to null to clear it.
/// </summary>
public sealed record KdpStyleChanges(IReadOnlyDictionary<string, string?>? Speakers = null, KdpMainCharacterChange? MainCharacter = null, KdpTitlePictureChange? TitlePicture = null)
{
    public bool IsEmpty => Speakers is not { Count: > 0 } && MainCharacter is null && TitlePicture is null;

    /// <summary>
    /// The settings with these choices added. A speaker or main character that is not in the cast, a colour that fails the colour rules that can be checked without the
    /// book's pictures (readable on white, not red, not close to a triplet colour) or a title picture page the book does not have is refused with the reason. The tint
    /// and distinctness rules are checked once the pictures are known (see <see cref="KdpStyleOutcome.RejectedSpeakers"/>).
    /// </summary>
    /// <exception cref="KdpException">A choice is refused.</exception>
    public KdpBookSettings ApplyTo(KdpBookSettings settings, IReadOnlyList<string> cast, int storyPages)
    {
        try
        {
            settings = ApplySpeakers(settings, cast);
            settings = ApplyMain(settings, cast);
            return ApplyTitle(settings, storyPages);
        }
        catch (KdpSettingsException ex)
        {
            throw new KdpException(ex.Message, ex);
        }
    }

    private KdpBookSettings ApplySpeakers(KdpBookSettings settings, IReadOnlyList<string> cast)
    {
        foreach (var (name, color) in Speakers ?? new Dictionary<string, string?>())
        {
            if (color is not null)
            {
                EnsureInCast("--speaker", name, cast);
            }

            if (color is not null && SpeakerColourRules.Check(color, [], []) is { } why)
            {
                throw new KdpException($"--speaker {name}={color} is refused: {why}");
            }

            settings = settings.WithSpeaker(name, color);
        }

        return settings;
    }

    private KdpBookSettings ApplyMain(KdpBookSettings settings, IReadOnlyList<string> cast)
    {
        if (MainCharacter is null)
        {
            return settings;
        }

        if (MainCharacter.Name is not null)
        {
            EnsureInCast("--main-character", MainCharacter.Name, cast);
        }

        return settings.WithMainCharacter(MainCharacter.Name, MainCharacter.Reason);
    }

    private KdpBookSettings ApplyTitle(KdpBookSettings settings, int storyPages)
    {
        if (TitlePicture is null)
        {
            return settings;
        }

        if (TitlePicture.Picture is { } picture && picture.Page > storyPages)
        {
            throw new KdpException($"--title-picture names page {picture.Page}, but the book has {storyPages} story pages with a picture.");
        }

        return settings.WithTitlePicture(TitlePicture.Picture);
    }

    private static void EnsureInCast(string option, string name, IReadOnlyList<string> cast)
    {
        if (!cast.Contains(name, StringComparer.Ordinal))
        {
            var known = cast.Count == 0 ? "the book has no cast" : $"the cast is: {string.Join(", ", cast)}";
            throw new KdpException($"{option} names \"{name}\", who is not in the book's cast ({known}).");
        }
    }
}
