using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaveTheDate.Data;
using SaveTheDate.Domain;
using SaveTheDate.Services;
using SkiaSharp;

namespace SaveTheDate.Tests;

public class TestApplication : WebApplicationFactory<Program>
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "savethedate-http-" + Guid.NewGuid());
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Root);
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(Root, "http.db")};Pooling=False");
        builder.UseSetting("Database:ApplyMigrations", "true");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.ConfigureServices(services => { services.RemoveAll<IImageStorage>(); services.AddSingleton<IImageStorage>(new LocalImageStorage(new TestEnvironment(Root))); });
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
}

public sealed class HttpFlowTests : IClassFixture<TestApplication>
{
    readonly TestApplication app;
    public HttpFlowTests(TestApplication application) => app = application;
    static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    static async Task<HttpResponseMessage> Post(HttpClient client, string path, string token, params (string Key, string Value)[] values) =>
        await client.PostAsync(path, new FormUrlEncodedContent(values.Append(("__RequestVerificationToken", token)).ToDictionary(x => x.Item1, x => x.Item2)));

    [Fact]
    public async Task Visitors_can_discover_the_product_before_signing_in()
    {
        using var client = app.Browser();
        var landing = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, landing.StatusCode);
        var html = await landing.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/registro\"", html);
        Assert.Contains("href=\"/ingresar\"", html);
        Assert.DoesNotContain("action=\"/cuenta/ingresar\"", html);
        var login = await client.GetStringAsync("/ingresar");
        Assert.Contains("action=\"/cuenta/ingresar\"", login);
        Assert.NotEmpty(Token(login));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/eventos")).StatusCode);
    }

    [Fact]
    public async Task Register_sign_out_and_sign_in_work_with_real_cookies_and_csrf()
    {
        using var client = app.Browser();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var html = await client.GetStringAsync("/registro");
        var register = await Post(client, "/cuenta/registrar", Token(html), ("name", "Persona de prueba"), ("email", email), ("password", "a-long-test-password"), ("confirm", "a-long-test-password"));
        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode); Assert.Equal("/eventos", register.Headers.Location?.OriginalString);
        var dashboard = await client.GetStringAsync("/eventos");
        Assert.Contains("Mis eventos", dashboard);
        Assert.DoesNotContain("Mili &amp; Armando", dashboard);
        var landing = await client.GetStringAsync("/");
        Assert.Contains("href=\"/eventos\"", landing);
        Assert.DoesNotContain("href=\"/registro\"", landing);
        var logout = await Post(client, "/cuenta/salir", Token(dashboard)); Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var unauthorized = await client.GetAsync("/eventos"); Assert.Equal(HttpStatusCode.Redirect, unauthorized.StatusCode);
        html = await client.GetStringAsync("/ingresar");
        var login = await Post(client, "/cuenta/ingresar", Token(html), ("email", email), ("password", "a-long-test-password"));
        Assert.Equal("/eventos", login.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/eventos")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_post_without_csrf_or_use_development_demo()
    {
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/cuenta/ingresar", new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "x@example.test", ["password"] = "bad" }))).StatusCode);
        Assert.Contains((await client.PostAsync("/cuenta/demo", null)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/i/no-such-event")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/media/" + Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Publication_controls_images_and_calendar_and_rsvp_reaches_owner()
    {
        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var ownerId = Guid.NewGuid().ToString();
        await using (var db = await factory.CreateDbContextAsync()) { db.Users.Add(new ApplicationUser { Id = ownerId, UserName = ownerId }); await db.SaveChangesAsync(); }
        var events = new EventService(factory, new TestUser(ownerId));
        var item = await events.CreateAsync("Evento HTTP", EventKind.Wedding, new DateTime(2027, 3, 4, 18, 0, 0));
        var gallery = new GalleryService(factory, new TestUser(ownerId), scope.ServiceProvider.GetRequiredService<IImageStorage>());
        using var bitmap = new SKBitmap(10, 10); bitmap.Erase(SKColors.Green);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var stream = new MemoryStream(data.ToArray());
        await gallery.AddAsync(item.Id, stream, "Una foto privada");
        var photo = (await events.GetAsync(item.Id))!.Gallery.Images.Single();
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/media/{photo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/i/{item.Slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/i/{item.Slug}/calendario")).StatusCode);
        item.IsPublished = true; await events.SaveAsync(item, item.LocalDate);
        var media = await client.GetAsync($"/media/{photo.Id}"); Assert.Equal(HttpStatusCode.OK, media.StatusCode); Assert.Equal("image/webp", media.Content.Headers.ContentType?.MediaType);
        var calendar = await client.GetStringAsync($"/i/{item.Slug}/calendario"); Assert.Contains("BEGIN:VCALENDAR", calendar);
        var html = await client.GetStringAsync($"/i/{item.Slug}");
        Assert.DoesNotContain("notice error", html);
        Assert.Contains($"/i/{item.Slug}#invitation-details", html);
        var response = await Post(client, $"/i/{item.Slug}/confirmar", Token(html), ("name", "Invitado HTTP"), ("email", "guest-http@example.test"), ("attending", "yes"), ("size", "3"), ("note", "¡Ahí estaremos!"));
        Assert.Contains("respuesta=ok", response.Headers.Location!.OriginalString);
        var confirmation = await client.GetStringAsync(response.Headers.Location);
        Assert.Contains("rsvp-success", confirmation);
        Assert.DoesNotContain("rsvp-form", confirmation);
        Assert.Equal(3, (await events.GetAsync(item.Id))!.Responses.Single().PartySize);
        var publicHtml = await client.GetStringAsync($"/i/{item.Slug}"); Assert.DoesNotContain("guest-http@example.test", publicHtml);
        item.IsPublished = false; await events.SaveAsync(item, item.LocalDate);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/media/{photo.Id}")).StatusCode);
    }
}
