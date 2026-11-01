namespace PrintTheDamnBook.Core.Text;

/// <summary>Why a book reads the way it does (Slice 17), for the switch's tooltip and the sub-heading.</summary>
public enum DirectionSource
{
    Picked,
    Language,
    Guessed,
}

/// <summary>
/// The reader's pick of a book's reading direction. A click on either option is a pick (even on the one already shown); a pick is never
/// replaced by the language or the guess; changing a Dino book's language clears it. Without a pick the direction follows
/// <see cref="TextDirection.Resolve"/>.
/// </summary>
public sealed class BookDirectionChoice
{
    /// <summary>The reader's pick, or null until they make one (saved in <c>book.json</c>).</summary>
    public ReadingDirection? Picked { get; private set; }

    public void Pick(ReadingDirection direction)
    {
        Picked = direction;
    }

    /// <summary>Takes back a pick loaded from <c>book.json</c> (null for none).</summary>
    public void Restore(ReadingDirection? saved)
    {
        Picked = saved;
    }

    /// <summary>A language change hands the direction back to the language.</summary>
    public void LanguageChanged()
    {
        Picked = null;
    }

    public ReadingDirection Resolve(string? dinoLanguage, IEnumerable<string?> textsInBookOrder)
    {
        return TextDirection.Resolve(Picked, dinoLanguage, textsInBookOrder);
    }

    public DirectionSource Source(bool isDinoBook)
    {
        if (Picked is not null)
        {
            return DirectionSource.Picked;
        }

        return isDinoBook ? DirectionSource.Language : DirectionSource.Guessed;
    }
}
