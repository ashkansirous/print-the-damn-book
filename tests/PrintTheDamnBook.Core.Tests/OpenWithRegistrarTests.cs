using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.Core.Tests;

public class OpenWithRegistrarTests
{
    private sealed class FakeStore : IOpenWithStore
    {
        public Dictionary<(string Key, string? Name), string> Values { get; } = new();

        public string? Get(string key, string? name) => Values.GetValueOrDefault((key, name));

        public void Set(string key, string? name, string value) => Values[(key, name)] = value;
    }

    private const string Exe = @"C:\Apps\PrintTheDamnBook.exe";

    [Fact]
    public void Plan_writes_only_under_the_classes_hive_and_never_user_choice()
    {
        var plan = OpenWithRegistrar.Plan(Exe);

        Assert.All(plan, v => Assert.StartsWith(@"Software\Classes\", v.Key));
        Assert.DoesNotContain(plan, v => v.Key.Contains("UserChoice", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(plan, v => v.Key == @"Software\Classes\.dino" && v.Name is null);
    }

    [Fact]
    public void Plan_lists_the_three_registrations()
    {
        var plan = OpenWithRegistrar.Plan(Exe);

        Assert.Contains(plan, v => v.Key == @"Software\Classes\.dino\OpenWithProgids" && v.Name == "PrintTheDamnBook.dino");
        Assert.Contains(plan, v => v.Key == @"Software\Classes\PrintTheDamnBook.dino\shell\open\command" && v.Value == @"""C:\Apps\PrintTheDamnBook.exe"" ""%1""");
        Assert.Contains(plan, v => v.Key == @"Software\Classes\Applications\PrintTheDamnBook.exe\SupportedTypes" && v.Name == ".dino");
    }

    [Fact]
    public void Second_run_writes_nothing()
    {
        var store = new FakeStore();
        var first = OpenWithRegistrar.Apply(store, Exe);

        Assert.Equal(OpenWithRegistrar.Plan(Exe).Count, first);
        Assert.Equal(0, OpenWithRegistrar.Apply(store, Exe));
    }

    [Fact]
    public void Moved_exe_rewrites_only_the_paths()
    {
        var store = new FakeStore();
        OpenWithRegistrar.Apply(store, Exe);

        var written = OpenWithRegistrar.Apply(store, @"D:\New\PrintTheDamnBook.exe");

        Assert.Equal(3, written);
        Assert.Equal(@"""D:\New\PrintTheDamnBook.exe"" ""%1""", store.Get(@"Software\Classes\PrintTheDamnBook.dino\shell\open\command", null));
    }

    [Theory]
    [InlineData(@"C:\Program Files\PrintTheDamnBook\PrintTheDamnBook.exe", true)]
    [InlineData(@"C:\Users\a\Downloads\PrintTheDamnBook.exe", true)]
    [InlineData(@"D:\repo\src\App\bin\Debug\net10.0-windows\PrintTheDamnBook.exe", false)]
    [InlineData(@"D:\repo\src\App\OBJ\x\PrintTheDamnBook.exe", false)]
    [InlineData(@"D:\repo\src\App\bin\RELEASE\net10.0-windows\PrintTheDamnBook.exe", false)]
    [InlineData(@"C:\tools\bin\PrintTheDamnBook.exe", true)]
    [InlineData(@"C:\Program Files\WindowsApps\Pkg_1.0_x64__abc\PrintTheDamnBook.exe", false)]
    public void Only_a_real_install_location_is_registered(string exe, bool expected)
    {
        Assert.Equal(expected, OpenWithRegistrar.IsRegistrableLocation(exe));
    }
}
