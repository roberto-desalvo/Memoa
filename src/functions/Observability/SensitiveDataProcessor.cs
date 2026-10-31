using System.Diagnostics;
using OpenTelemetry;

namespace Memoa.Functions.Observability;

/// <summary>
/// Toglie dagli span gli attributi che possono contenere dati personali o segreti: indirizzi IP,
/// query string (es. token SAS) e testo delle query SQL.
/// </summary>
public sealed class SensitiveDataProcessor : BaseProcessor<Activity>
{
    private static readonly string[] RemovedTags =
    [
        "client.address",
        "http.client_ip",
        "net.peer.ip",
        "url.query",
        "db.query.text",
        "db.statement",
        "enduser.id",
    ];

    public override void OnEnd(Activity activity)
    {
        foreach (var tag in RemovedTags)
        {
            if (activity.GetTagItem(tag) is not null)
            {
                activity.SetTag(tag, null);
            }
        }

        if (activity.GetTagItem("url.full") is string url && url.Contains('?'))
        {
            activity.SetTag("url.full", url[..url.IndexOf('?')]);
        }
    }
}
