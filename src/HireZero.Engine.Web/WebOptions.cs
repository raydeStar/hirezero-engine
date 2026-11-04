using System.Net;
using System.Text.Json;
using HireZero.Core;

namespace HireZero.Engine.Web;

public sealed record WebOptions(string Mode, int Port, string? Data, string? Gallery, Uri? Origin)
{
    public static WebOptions Parse(string[] args)
    {
        var flags = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i += 2)
            if (
                i + 1 >= args.Length
                || args[i] is not ("--mode" or "--port" or "--data" or "--gallery")
                || !flags.TryAdd(args[i], args[i + 1])
            )
                throw new ArgumentException("Expected --mode, --port, --data and/or --gallery.");
        var mode = flags.GetValueOrDefault("--mode") ?? "gallery";
        if (
            mode is not ("gallery" or "rehearsal" or "worker")
            || !int.TryParse(flags.GetValueOrDefault("--port") ?? "5077", out var port)
            || port is < 1 or > 65535
        )
            throw new ArgumentException("Choose gallery, rehearsal or worker mode and a valid loopback port.");
        string? Folder(string name)
        {
            if (!flags.TryGetValue(name, out var path))
                return null;
            if (!Path.IsPathFullyQualified(path))
                throw new ArgumentException(name + " needs an absolute path.");
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        var data = Folder("--data");
        var gallery = Folder("--gallery");
        if (
            mode == "gallery" && (data is not null || gallery is null)
            || mode == "worker" && (data is null || gallery is not null)
            || mode == "rehearsal" && data is null
        )
            throw new ArgumentException(
                "Gallery requires --gallery only. Worker requires --data only. Rehearsal requires --data and may read --gallery."
            );
        if (data is not null && gallery is not null && (Files.Inside(data, gallery) || Files.Inside(gallery, data)))
            throw new ArgumentException("Keep the gallery and private records in separate directories.");
        var address = Environment.GetEnvironmentVariable("HIREZERO_WEB_ORIGIN");
        Uri? origin = null;
        if (mode == "worker" || mode == "gallery" && address is not null)
        {
            if (
                !Uri.TryCreate(address, UriKind.Absolute, out origin)
                || origin.Scheme != "https"
                || origin.UserInfo.Length != 0
                || origin.PathAndQuery != "/"
                || origin.Fragment.Length != 0
            )
                throw new ArgumentException(
                    "Set HIREZERO_WEB_ORIGIN to the public HTTPS origin served by the loopback reverse proxy."
                );
        }
        return new(mode, port, data, gallery, origin);
    }

    public bool Accepts(HttpRequest request)
    {
        if (Origin is not null)
            return string.Equals(Origin.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)
                && (request.IsHttps || request.Headers["X-Forwarded-Proto"] == "https");
        return request.Host.Host is "localhost" or "127.0.0.1" && (Port == 0 || request.Host.Port == Port);
    }
}

internal static class Files
{
    public static bool Inside(string child, string parent) =>
        Path.GetFullPath(child).Equals(Path.GetFullPath(parent), StringComparison.OrdinalIgnoreCase)
        || Path.GetFullPath(child)
            .StartsWith(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );

    public static bool Plain(string path)
    {
        for (
            FileSystemInfo? f = new FileInfo(path);
            f is not null;
            f = f is DirectoryInfo dir ? dir.Parent : ((FileInfo)f).Directory
        )
            if (f.LinkTarget is not null || f.Exists && f.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return false;
        return true;
    }

    public static byte[]? Read(string path, int maximum)
    {
        if (!File.Exists(path) || !Plain(path))
            return null;
        using var input = File.OpenRead(path);
        if (input.Length > maximum)
            return null;
        var bytes = new byte[(int)input.Length];
        input.ReadExactly(bytes);
        return bytes;
    }

    public static T? ReadJson<T>(string path, int maximum)
        where T : class =>
        Read(path, maximum) is { } bytes ? JsonSerializer.Deserialize<T>(bytes, CompanyJson.Options) : null;

    public static void NewJson(string path, object value)
    {
        // A completed JSON record becomes visible all at once. Existing records can never be overwritten.
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, value, CompanyJson.Options);
                file.Flush(flushToDisk: true);
            }
            File.Move(tmp, path, overwrite: false);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    public static byte[]? Jpeg(string path)
    {
        var data = Read(path, 256 * 1024);
        return
            data is { Length: > 4 }
            && data[0] == 255
            && data[1] == 216
            && data[2] == 255
            && data[^2] == 255
            && data[^1] == 217
            ? data
            : null;
    }
}
