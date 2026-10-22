using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Printing;

/// <summary>An entry of an options picker.</summary>
public sealed record LayoutChoice<T>(T Value, string Label);

public static class LayoutChoices
{
    public static IReadOnlyList<LayoutChoice<ScalingMode>> Scaling { get; } =
    [
        new(ScalingMode.Fit, "Fit (whole page)"),
        new(ScalingMode.Fill, "Fill and crop"),
    ];

    public static IReadOnlyList<LayoutChoice<FlipEdge>> Flip { get; } =
    [
        new(FlipEdge.ShortEdge, "Short edge"),
        new(FlipEdge.LongEdge, "Long edge (backs turned 180°)"),
    ];
}
