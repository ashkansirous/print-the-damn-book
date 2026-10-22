using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests;

public class LayoutBackRotationTests
{
    private static Sheet MakeSheet(int number)
    {
        return new Sheet(number, MakeFace($"f{number}"), MakeFace($"b{number}"));
    }

    private static SheetFace MakeFace(string name)
    {
        return new SheetFace(new BookPage(null, name + "L", PageKind.Numbered), new BookPage(null, name + "R", PageKind.Numbered));
    }

    [Fact]
    public void Only_a_long_edge_flip_rotates_the_backs()
    {
        Assert.False(AutomaticDuplexPlan.RotatesBacks(FlipEdge.ShortEdge));
        Assert.True(AutomaticDuplexPlan.RotatesBacks(FlipEdge.LongEdge));
    }

    [Theory]
    [InlineData(FlipEdge.ShortEdge, false)]
    [InlineData(FlipEdge.LongEdge, true)]
    public void Faces_alternate_front_then_back_and_only_backs_rotate(FlipEdge flip, bool backsRotate)
    {
        var faces = AutomaticDuplexPlan.Build([MakeSheet(1), MakeSheet(2)], flip);

        Assert.Equal(4, faces.Count);
        Assert.Equal(["f1L", "b1L", "f2L", "b2L"], faces.Select(f => f.Face.Left.Label).ToArray());
        Assert.Equal([false, backsRotate, false, backsRotate], faces.Select(f => f.Rotate180).ToArray());
        Assert.All(faces, f => Assert.False(f.MarksFeedEdge));
    }

    [Fact]
    public void Page_assignment_is_the_same_for_both_flips()
    {
        var sheets = new[] { MakeSheet(1), MakeSheet(2) };

        var shortEdge = AutomaticDuplexPlan.Build(sheets, FlipEdge.ShortEdge).Select(f => f.Face);
        var longEdge = AutomaticDuplexPlan.Build(sheets, FlipEdge.LongEdge).Select(f => f.Face);

        Assert.Equal(shortEdge, longEdge);
    }
}
