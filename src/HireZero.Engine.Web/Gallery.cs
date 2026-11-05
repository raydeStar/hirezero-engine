using System.Text.Json;
using HireZero.Core;

namespace HireZero.Engine.Web;

internal sealed record GalleryLimits(int? Seconds, int? Requests, int? Bytes)
{
    public bool Valid =>
        Seconds is >= 1 and <= 120 && Requests is >= 1 and <= 100 && Bytes is >= 1 and <= 16 * 1024 * 1024;
}

internal sealed record GallerySummary(
    string Title,
    string Goal,
    string Site,
    string Answer,
    string Source,
    string State,
    string Teardown,
    long? Tokens,
    long? WallMilliseconds,
    string[] BlockedOrigins,
    BrowserStep[]? Steps = null,
    GalleryLimits? Limits = null,
    int? AllowedRequests = null,
    string? Provider = null,
    string? CapturedText = null,
    int? RefusedRequests = null,
    string? CaptureCredit = null
);

internal sealed class Gallery(string? root)
{
    private static bool Slug(string name) =>
        name.Length is > 0 and <= 64
        && char.IsAsciiLetterLower(name[0])
        && name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

    private static bool Fits(string? s, int length) => s is not null && s.Length <= length;

    private GallerySummary? Summary(string slug)
    {
        if (root is null || !Slug(slug))
            return null;
        try
        {
            var s = Files.ReadJson<GallerySummary>(Path.Combine(root, slug, "summary.json"), 64 * 1024);
            return
                s is not null
                && Fits(s.Title, 200)
                && Fits(s.Goal, 1000)
                && Fits(s.Site, 300)
                && Fits(s.Source, 120)
                && Fits(s.State, 80)
                && Fits(s.Answer, 16000)
                && Fits(s.Teardown, 200)
                && s.Tokens is not < 0
                && s.WallMilliseconds is not < 0
                && s.BlockedOrigins is { Length: <= 40 }
                && s.BlockedOrigins.All(o => Fits(o, 300))
                && (
                    s.Steps is null
                    || s.Steps.Length <= 16
                        && s.Steps.All(x => x is not null && Fits(x.Action, 20) && Fits(x.Value, 2000))
                )
                && s.Provider?.Length is not > 120
                && s.CapturedText?.Length is not > 8000
                && s.AllowedRequests is not < 0
                && s.RefusedRequests is not < 0
                && s.CaptureCredit?.Length is not > 400
                ? s
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public object[] List()
    {
        if (root is null || !Directory.Exists(root) || !Files.Plain(root))
            return [];
        return Directory
            .EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(Slug)
            .Order()
            .Take(64)
            .Select(slug =>
                Summary(slug) is { } s
                    ? new
                    {
                        slug,
                        s.Title,
                        s.Goal,
                        s.Site,
                        s.Answer,
                        s.Source,
                        s.State,
                        s.Teardown,
                        s.Tokens,
                        s.WallMilliseconds,
                        s.BlockedOrigins,
                        s.Steps,
                        limits = s.Limits is { Valid: true } limits ? limits : null,
                        s.AllowedRequests,
                        s.RefusedRequests,
                        s.CaptureCredit,
                        s.Provider,
                        s.CapturedText,
                        screenshotUrl = ImageFile(slug) is null ? null : "/api/gallery/" + slug + "/capture.jpg",
                    }
                    : null
            )
            .OfType<object>()
            .ToArray();
    }

    private byte[]? ImageFile(string slug) => Files.Jpeg(Path.Combine(root!, slug, "capture.jpg"));

    public byte[]? Image(string slug) => Summary(slug) is null ? null : ImageFile(slug);
}
