using System.IO;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.App.Services;

/// <summary>The command line this process was started with, read once (Slice 19). The parsing and its rules live in Core's <see cref="HandoverArguments"/>.</summary>
public static class LaunchArguments
{
    /// <summary>The hand-over from <paramref name="args"/> (the arguments after the program name), or null when the app was started plainly.</summary>
    public static HandoverRequest? Read(IReadOnlyList<string> args)
    {
        var request = HandoverArguments.Parse(args, File.Exists);
        System.Diagnostics.Debug.WriteLine(request is null
            ? "[handover] no .dino argument, plain start"
            : $"[handover] request file={request.FileName} usable={request.IsUsable} language={request.Language ?? "(none)"}");
        return request;
    }
}
