using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Tests;

public class HandoverArgumentsTests
{
    private static readonly Func<string, bool> Exists = _ => true;
    private static readonly Func<string, bool> Missing = _ => false;

    [Fact]
    public void No_arguments_is_no_handover()
    {
        Assert.Null(HandoverArguments.Parse([], Exists));
    }

    [Fact]
    public void Only_flags_is_no_handover()
    {
        Assert.Null(HandoverArguments.Parse(["--language", "fa", "--other"], Exists));
    }

    [Fact]
    public void Path_with_spaces_is_taken_whole()
    {
        var request = HandoverArguments.Parse([@"C:\My Books\Good Enough.dino"], Exists)!;

        Assert.Equal(@"C:\My Books\Good Enough.dino", request.Path);
        Assert.True(request.IsUsable);
        Assert.Equal("Good Enough.dino", request.FileName);
        Assert.Null(request.Language);
    }

    [Theory]
    [InlineData("--language", "fa")]
    [InlineData("--LANGUAGE", "fa")]
    public void Language_flag_with_a_separate_value(string flag, string code)
    {
        Assert.Equal("fa", HandoverArguments.Parse([@"C:\b.dino", flag, code], Exists)!.Language);
    }

    [Fact]
    public void Language_flag_with_equals_and_before_the_path()
    {
        var request = HandoverArguments.Parse(["--language=sv", @"C:\b.dino"], Exists)!;

        Assert.Equal("sv", request.Language);
        Assert.Equal(@"C:\b.dino", request.Path);
    }

    [Fact]
    public void Language_flag_without_a_value_is_ignored()
    {
        Assert.Null(HandoverArguments.Parse([@"C:\b.dino", "--language"], Exists)!.Language);
    }

    [Fact]
    public void Language_flag_never_swallows_the_path_or_another_flag()
    {
        var request = HandoverArguments.Parse(["--language", @"C:\b.dino"], Exists)!;
        Assert.Null(request.Language);
        Assert.True(request.IsUsable);

        Assert.Null(HandoverArguments.Parse(["--language", "--foo", @"C:\b.dino"], Exists)!.Language);
    }

    [Fact]
    public void First_dino_argument_wins_over_an_earlier_plain_one()
    {
        var request = HandoverArguments.Parse(["notes.txt", @"C:\book.dino"], Exists)!;

        Assert.Equal(@"C:\book.dino", request.Path);
        Assert.True(request.IsUsable);
    }

    [Fact]
    public void Without_a_dino_argument_the_first_plain_one_is_reported()
    {
        Assert.Equal("notes.txt", HandoverArguments.Parse(["notes.txt", "other.txt"], Exists)!.Path);
    }

    [Fact]
    public void Unknown_flags_are_ignored()
    {
        var request = HandoverArguments.Parse(["--verbose", @"C:\b.dino", "--x=1"], Exists)!;

        Assert.True(request.IsUsable);
        Assert.Equal(@"C:\b.dino", request.Path);
    }

    [Fact]
    public void Missing_file_is_not_usable_but_is_still_reported()
    {
        var request = HandoverArguments.Parse([@"C:\gone.dino"], Missing)!;

        Assert.False(request.IsUsable);
        Assert.Equal("gone.dino", request.FileName);
    }

    [Theory]
    [InlineData(@"C:\notes.txt")]
    [InlineData(@"C:\book.dino.zip")]
    [InlineData(@"C:\folder")]
    public void Wrong_extension_is_not_usable(string path)
    {
        Assert.False(HandoverArguments.Parse([path], Exists)!.IsUsable);
    }

    [Theory]
    [InlineData(@"\server\share\b.dino")]
    [InlineData(@"\?\C:\b.dino")]
    [InlineData(@"b.dino")]
    [InlineData(@"\b.dino")]
    public void Network_device_and_relative_paths_are_not_usable(string path)
    {
        Assert.False(HandoverArguments.Parse([path], Exists)!.IsUsable);
    }

    [Fact]
    public void Extension_is_case_insensitive()
    {
        Assert.True(HandoverArguments.Parse([@"D:\Book.DINO"], Exists)!.IsUsable);
    }
}
