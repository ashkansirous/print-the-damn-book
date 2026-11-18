using PrintTheDamnBook.Core.Text;

namespace PrintTheDamnBook.Core.Layout;

/// <summary>
/// Where a page's words are printed and whether the user chose that. A page the user did not choose follows the fit: words that do not
/// fit the band even at its smallest size move to a page of their own, and move back when they fit again. A page the user chose
/// (by flipping the switch in the Check step) is never moved.
/// </summary>
public readonly record struct StoryPlacement(TextPlacement Placement, bool ChosenByUser)
{
    /// <summary>The placement the page should have, given whether its words fit the band at the smallest size.</summary>
    public StoryPlacement Resolve(bool fitsBand)
    {
        if (ChosenByUser)
        {
            return this;
        }

        return this with { Placement = fitsBand ? TextPlacement.OnPicture : TextPlacement.OwnPage };
    }

    /// <summary>The user flipped the switch: this placement stays whatever the fit says.</summary>
    public StoryPlacement ChosenAs(TextPlacement placement) => new(placement, true);
}
