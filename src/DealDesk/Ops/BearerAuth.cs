using System.Security.Cryptography;
using System.Text;

namespace DealDesk.Ops;

/// The static bearer token on write endpoints — the last part of SPEC.md
/// feature 7. One shared token, no users, no roles: v1's non-goals refuse
/// anything more, and the appraiser name on the worksheet is who did the work.
///
/// Reads stay open. A GET tells a caller what the desk already knows; a POST,
/// PUT, PATCH or DELETE changes it, and only those four are guarded. That is
/// also why the guard sits inside the ops recording — a refused write is still
/// a request an operator wants counted.
///
/// **An unset token means the guard is off**, and `Program.cs` reads it from
/// `Auth:Token` (so `DEALDESK_Auth__Token` in the environment). A fresh clone
/// runs its quickstart with no configuration step, exactly as it did before
/// this shipped, and a deployment that sets the value gets the guard. The
/// alternative — refusing every write until something is configured — would
/// mean the README's five-minute path started with a token to invent.
public static class BearerAuth
{
    private const string Scheme = "Bearer ";

    /// The four methods that change something, and therefore the four the
    /// token guards.
    public static bool GuardsMethod(string? method) =>
        HttpMethods.IsPost(method ?? string.Empty)
        || HttpMethods.IsPut(method ?? string.Empty)
        || HttpMethods.IsPatch(method ?? string.Empty)
        || HttpMethods.IsDelete(method ?? string.Empty);

    /// True when this `Authorization` header value carries exactly this token.
    ///
    /// Compared in fixed time, so a caller cannot learn the token one character
    /// at a time by watching how long the refusal takes. `FixedTimeEquals`
    /// answers false for differing lengths, which is the one thing about a
    /// token a timing attack can read anyway.
    public static bool Presented(string? authorization, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var presented = authorization[Scheme.Length..].Trim();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(token));
    }

    /// Refuses unauthenticated writes with 401 once a token is configured. A
    /// null or blank token leaves the pipeline untouched.
    public static IApplicationBuilder UseBearerTokenOnWrites(
        this IApplicationBuilder app,
        string? token)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (string.IsNullOrWhiteSpace(token))
        {
            return app;
        }

        var expected = token.Trim();

        return app.Use(async (context, next) =>
        {
            if (!GuardsMethod(context.Request.Method)
                || Presented(context.Request.Headers.Authorization, expected))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            // 401 rather than 403 for both a missing and a wrong token: the
            // credential is the problem, not the permission, and dealdesk has
            // exactly one credential to offer.
            context.Response.Headers.WWWAuthenticate = "Bearer";

            await Results.Json(
                new { error = "write requests need an Authorization: Bearer token" },
                statusCode: StatusCodes.Status401Unauthorized)
                .ExecuteAsync(context)
                .ConfigureAwait(false);
        });
    }
}
