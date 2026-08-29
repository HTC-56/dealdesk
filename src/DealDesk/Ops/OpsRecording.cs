using System.Diagnostics;

namespace DealDesk.Ops;

/// Wires the two observers of SPEC.md feature 7 into the request pipeline:
/// every response bumps a metric series and writes a ledger line.
///
/// This sits OUTSIDE the bearer-token check on purpose. A refused write is
/// exactly the request an operator most wants to see, so the 401 is counted
/// and written down like any other answer.
public static class OpsRecording
{
    /// Times each request and records it once, after the response status is
    /// settled. The recording itself is never allowed to fail a request: it
    /// runs after `next`, and the ledger swallows its own write errors.
    public static IApplicationBuilder UseOpsRecording(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var started = Stopwatch.GetTimestamp();

            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

                var metrics = context.RequestServices.GetRequiredService<OpsMetrics>();
                metrics.Observe(context.Request.Method, context.Response.StatusCode, elapsed);

                var ledger = context.RequestServices.GetRequiredService<OpsLedger>();
                ledger.Append(
                    context.Request.Method,
                    context.Request.Path.Value ?? "/",
                    context.Response.StatusCode,
                    elapsed);
            }
        });
    }
}
