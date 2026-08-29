using System.Reflection;
using System.Text;

namespace DealDesk.Api;

/// `GET /` — the desk page of SPEC.md feature 6.
///
/// One hand-written HTML file with its CSS and its JavaScript inline: no
/// framework, no build step, no CDN, no web font, no image. That is not
/// minimalism for its own sake. dealdesk's whole promise is that it makes zero
/// outbound requests (SPEC.md non-goals), and a page that pulled a stylesheet
/// or a font from somewhere would break that promise in the one place a
/// reviewer actually looks — their browser's network tab.
///
/// The file rides inside the assembly exactly as the migrations and the seed do
/// (`src/DealDesk/DealDesk.csproj`), so the single-file publish of feature 9
/// carries its own user interface with no sidecar directory to deploy.
///
/// Like `/healthz` and `/metrics`, this route is open even when the bearer
/// token is armed: the page is a reader, and every write it makes goes through
/// the same guarded routes any other caller uses.
public static class PageEndpoints
{
    /// The logical name the csproj gives `Page/index.html`.
    internal const string PageResource = "page.index.html";

    internal const string HtmlContentType = "text/html; charset=utf-8";

    /// Read once, on first request, and held for the life of the process: the
    /// page is a compile-time constant, so re-reading the manifest stream per
    /// request would buy nothing.
    private static readonly Lazy<string> Document = new(ReadDocument);

    public static void MapPageEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet("/", () => Results.Text(Document.Value, HtmlContentType));
    }

    /// The page as it was embedded. Missing means the build dropped the
    /// resource, which is a broken binary rather than a bad request — so it
    /// throws at first read instead of serving an empty document.
    internal static string ReadDocument()
    {
        var assembly = typeof(PageEndpoints).Assembly;
        using var stream = assembly.GetManifestResourceStream(PageResource)
            ?? throw new InvalidOperationException(
                $"Desk page resource '{PageResource}' is missing from {assembly.GetName().Name}.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
