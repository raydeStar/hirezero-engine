using System.Security.Cryptography;
using System.Text;
using HireZero.Core;

namespace HireZero.Engine.Web;

internal sealed class OwnerSessions
{
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] passphrase;
    private readonly WebOptions options;
    private readonly string cookie;

    private sealed record Login(string Nonce, DateTimeOffset Until);

    private Login? active;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(40);

    public OwnerSessions(WebOptions options, string? secret)
    {
        if (secret is null || secret.Length is < 16 or > 256)
            throw new ArgumentException(
                "Set HIREZERO_WEB_PASSPHRASE to 16–256 characters in the owner process environment."
            );
        passphrase = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        this.options = options;
        cookie = options.Mode == "worker" ? "__Host-HireZeroOwner" : "HireZeroRehearsal" + options.Port;
    }

    private string SetCookie(HttpContext ctx)
    {
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        ctx.Response.Cookies.Append(
            cookie,
            nonce,
            new()
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Strict,
                Secure = options.Mode == "worker",
                Path = options.Mode == "worker" ? "/" : "/api",
                MaxAge = Lifetime,
            }
        );
        return nonce;
    }

    private string? Nonce(HttpContext ctx) =>
        ctx.Request.Cookies[cookie] is { Length: 64 } n && n.All(Uri.IsHexDigit) ? n : null;

    private byte[] Mac(string n) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(n));

    private bool Authenticated(string? nonce) =>
        Volatile.Read(ref active) is { } a && nonce == a.Nonce && a.Until > DateTimeOffset.UtcNow;

    private object View(string n) =>
        new
        {
            mode = options.Mode,
            authenticated = Authenticated(n),
            csrf = Convert.ToHexStringLower(Mac(n)),
        };

    public object State(HttpContext ctx) => View(Nonce(ctx) ?? SetCookie(ctx));

    public string Require(HttpContext ctx)
    {
        var nonce = Nonce(ctx);
        if (!Authenticated(nonce))
            throw new WorkspaceException("Owner login required.", 401);
        return ContentGuard.Digest(nonce!);
    }

    public bool Csrf(HttpContext ctx)
    {
        var expectedOrigin = options.Origin?.GetLeftPart(UriPartial.Authority) ?? "http://" + ctx.Request.Host;
        if (ctx.Request.Headers.Origin is { Count: > 0 } origin && origin != expectedOrigin)
            return false;
        return Nonce(ctx) is { } nonce
            && ctx.Request.Headers["X-HireZero-CSRF"].ToString() is { Length: 64 } csrf
            && csrf.All(Uri.IsHexDigit)
            && CryptographicOperations.FixedTimeEquals(Mac(nonce), Convert.FromHexString(csrf));
    }

    public object SignIn(HttpContext ctx, string? secret)
    {
        if (
            secret is null
            || secret.Length > 256
            || !CryptographicOperations.FixedTimeEquals(passphrase, SHA256.HashData(Encoding.UTF8.GetBytes(secret)))
        )
            throw new WorkspaceException("Passphrase refused.", 401);
        var nonce = SetCookie(ctx);
        Volatile.Write(ref active, new(nonce, DateTimeOffset.UtcNow + Lifetime));
        return View(nonce);
    }

    public object SignOut(HttpContext ctx)
    {
        var current = Volatile.Read(ref active);
        if (current?.Nonce == Nonce(ctx))
            Interlocked.CompareExchange(ref active, null, current);
        return View(SetCookie(ctx));
    }
}
