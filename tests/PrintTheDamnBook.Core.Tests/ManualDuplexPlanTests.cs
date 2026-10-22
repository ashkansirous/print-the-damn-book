using PrintTheDamnBook.Core;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.Core.Tests;

public class ManualDuplexPlanTests
{
    private static List<Sheet> MakeSheets(int count)
    {
        return Enumerable.Range(1, count).Select(n => new Sheet(n, MakeFace($"f{n}"), MakeFace($"b{n}"))).ToList();
    }

    private static SheetFace MakeFace(string name)
    {
        return new SheetFace(new BookPage(null, name + "L", PageKind.Numbered), new BookPage(null, name + "R", PageKind.Numbered));
    }

    private static string[] Names(IEnumerable<PlannedFace> faces)
    {
        return faces.Select(f => f.Face.Left.Label[..^1]).ToArray();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Defaults_print_fronts_then_backs_in_sheet_order_unrotated(int count)
    {
        var plan = ManualDuplexPlan.Build(MakeSheets(count), new ManualDuplexOptions());

        Assert.Equal(Enumerable.Range(1, count).Select(n => $"f{n}"), Names(plan.Fronts));
        Assert.Equal(Enumerable.Range(1, count).Select(n => $"b{n}"), Names(plan.Backs));
        Assert.All(plan.Fronts.Concat(plan.Backs), f => Assert.False(f.Rotate180));
        Assert.All(plan.Fronts.Concat(plan.Backs), f => Assert.False(f.MarksFeedEdge));
    }

    [Fact]
    public void Reverse_reverses_only_the_backs()
    {
        var plan = ManualDuplexPlan.Build(MakeSheets(3), new ManualDuplexOptions(ReverseBacks: true));

        Assert.Equal(["f1", "f2", "f3"], Names(plan.Fronts));
        Assert.Equal(["b3", "b2", "b1"], Names(plan.Backs));
    }

    [Fact]
    public void Rotate_turns_only_the_backs()
    {
        var plan = ManualDuplexPlan.Build(MakeSheets(2), new ManualDuplexOptions(RotateBacks: true));

        Assert.All(plan.Fronts, f => Assert.False(f.Rotate180));
        Assert.All(plan.Backs, f => Assert.True(f.Rotate180));
        Assert.Equal(["b1", "b2"], Names(plan.Backs));
    }

    [Fact]
    public void Reverse_and_rotate_combine()
    {
        var plan = ManualDuplexPlan.Build(MakeSheets(2), new ManualDuplexOptions(true, true));

        Assert.Equal(["b2", "b1"], Names(plan.Backs));
        Assert.All(plan.Backs, f => Assert.True(f.Rotate180));
    }

    [Fact]
    public void Feed_edge_mark_is_on_fronts_only()
    {
        var plan = ManualDuplexPlan.Build(MakeSheets(1), new ManualDuplexOptions(MarkFeedEdge: true));

        Assert.All(plan.Fronts, f => Assert.True(f.MarksFeedEdge));
        Assert.All(plan.Backs, f => Assert.False(f.MarksFeedEdge));
    }

    [Fact]
    public void Cover_and_rest_plan_as_separate_runs()
    {
        var sheets = MakeSheets(3);
        var options = new ManualDuplexOptions(ReverseBacks: true);

        var cover = ManualDuplexPlan.Build(sheets.Take(1).ToList(), options);
        var rest = ManualDuplexPlan.Build(sheets.Skip(1).ToList(), options);

        Assert.Equal(["f1"], Names(cover.Fronts));
        Assert.Equal(["b1"], Names(cover.Backs));
        Assert.Equal(["f2", "f3"], Names(rest.Fronts));
        Assert.Equal(["b3", "b2"], Names(rest.Backs));
    }

    [Fact]
    public void No_sheets_gives_empty_passes()
    {
        var plan = ManualDuplexPlan.Build([], new ManualDuplexOptions());

        Assert.Empty(plan.Fronts);
        Assert.Empty(plan.Backs);
    }

    [Fact]
    public void Backs_are_rotated_by_default()
    {
        Assert.True(ManualDuplexPlan.DefaultRotateBacks);
    }

    [Fact]
    public void Test_sheet_is_the_first_sheet_whose_back_has_two_images()
    {
        var sheets = new List<Sheet>
        {
            new(1, MakeFace("f1"), new SheetFace(BookPage.Blank, new BookPage("x.png", "x", PageKind.Numbered))),
            new(2, MakeFace("f2"), new SheetFace(new BookPage("a.png", "a", PageKind.Numbered), new BookPage("b.png", "b", PageKind.Numbered))),
        };

        Assert.Equal(2, ManualDuplexPlan.PickTestSheet(sheets)!.Number);
    }

    [Fact]
    public void Test_sheet_falls_back_to_sheet_one()
    {
        var sheets = MakeSheets(2);

        Assert.Equal(1, ManualDuplexPlan.PickTestSheet(sheets)!.Number);
        Assert.Null(ManualDuplexPlan.PickTestSheet([]));
    }
}
