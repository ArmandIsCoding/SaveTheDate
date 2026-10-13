using Microsoft.Extensions.Options;
using SaveTheDate.Domain;
using SaveTheDate.Services;

namespace SaveTheDate.Tests;

public sealed class GoogleMapsTests
{
    static GoogleMaps Maps(string key = "") => new(Options.Create(new GoogleMapsOptions { BrowserApiKey = key }));

    [Fact]
    public void Manual_address_uses_encoded_google_directions_without_an_api_key()
    {
        var module = new EventModule { Kind = ModuleKind.Location, LocationAddress = "Córdoba 10 & destino=otro, Argentina" };
        Assert.Null(Maps().EmbedUrl(module));
        Assert.Equal("https://www.google.com/maps/dir/?api=1&destination=C%C3%B3rdoba%2010%20%26%20destino%3Dotro%2C%20Argentina", GoogleMaps.DirectionsUrl(module));
    }

    [Fact]
    public void Selected_place_wins_over_address_and_old_link_and_is_embedded_on_google_only()
    {
        var module = new EventModule { Kind = ModuleKind.Location, GooglePlaceId = "ChIJ_test-123", LocationAddress = "Córdoba", LinkUrl = "https://example.test/old" };
        var maps = Maps("synthetic-test-key");
        Assert.Equal("https://www.google.com/maps/embed/v1/place?key=synthetic-test-key&q=place_id%3AChIJ_test-123&language=es", maps.EmbedUrl(module));
        Assert.Contains("destination_place_id=ChIJ_test-123", GoogleMaps.ButtonUrl(module));
        module.GooglePlaceId = "";
        Assert.Contains("q=C%C3%B3rdoba", maps.EmbedUrl(module));
        Assert.DoesNotContain("destination_place_id", GoogleMaps.ButtonUrl(module));
        module.LocationAddress = "";
        Assert.Null(maps.EmbedUrl(module));
        Assert.Equal(module.LinkUrl, GoogleMaps.ButtonUrl(module));
        module.LinkUrl = "javascript:alert(1)";
        Assert.Null(GoogleMaps.ButtonUrl(module));
    }

    [Fact]
    public void Empty_locations_and_other_module_types_never_load_a_map()
    {
        var maps = Maps("synthetic-test-key");
        Assert.Null(maps.EmbedUrl(new EventModule { Kind = ModuleKind.Location }));
        Assert.Null(maps.EmbedUrl(new EventModule { Kind = ModuleKind.Story, LocationAddress = "Córdoba" }));
    }
}
