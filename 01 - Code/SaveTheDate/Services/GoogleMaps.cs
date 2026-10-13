using Microsoft.Extensions.Options;
using SaveTheDate.Domain;

namespace SaveTheDate.Services;

public sealed class GoogleMapsOptions
{
    // This browser key is intentionally visible to Google widgets. Restrict it
    // by website referrer and API in Google Cloud; configure it outside Git.
    public string BrowserApiKey { get; set; } = "";
}

public sealed class GoogleMaps(IOptions<GoogleMapsOptions> options)
{
    public string BrowserApiKey => options.Value.BrowserApiKey.Trim();
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BrowserApiKey);
    public static bool HasLocation(EventModule module) =>
        module.Kind == ModuleKind.Location &&
        (!string.IsNullOrWhiteSpace(module.GooglePlaceId) || !string.IsNullOrWhiteSpace(module.LocationAddress));

    public string? EmbedUrl(EventModule module)
    {
        if (!IsConfigured || !HasLocation(module)) return null;
        var query = string.IsNullOrWhiteSpace(module.GooglePlaceId)
            ? module.LocationAddress.Trim() : "place_id:" + module.GooglePlaceId;
        return "https://www.google.com/maps/embed/v1/place?key=" + Uri.EscapeDataString(BrowserApiKey)
            + "&q=" + Uri.EscapeDataString(query) + "&language=es";
    }

    public static string? DirectionsUrl(EventModule module)
    {
        if (!HasLocation(module)) return null;
        var destination = string.IsNullOrWhiteSpace(module.LocationAddress)
            ? "Ubicación del evento" : module.LocationAddress.Trim();
        var url = "https://www.google.com/maps/dir/?api=1&destination=" + Uri.EscapeDataString(destination);
        return string.IsNullOrWhiteSpace(module.GooglePlaceId) ? url
            : url + "&destination_place_id=" + Uri.EscapeDataString(module.GooglePlaceId);
    }

    public static string? ButtonUrl(EventModule module) => DirectionsUrl(module)
        ?? (!string.IsNullOrWhiteSpace(module.LinkUrl) && ModuleCatalog.SafeUrl(module.LinkUrl) ? module.LinkUrl : null);
}
