using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaveTheDate.Data;
using SaveTheDate.Domain;
using SaveTheDate.Services;
using SkiaSharp;

namespace SaveTheDate.Tests;

public sealed class AdminTests : IDisposable
{
    private readonly TestApplication app = new();
    private const string TestPassword = "temporary-test-password";
    public void Dispose() => app.Dispose();

    private async Task<ApplicationUser> Account(IServiceProvider services, string name = "Mili y Armando")
    {
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = name };
        Assert.True((await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, TestPassword)).Succeeded);
        return user;
    }
    private static async Task<int> Permission(IServiceProvider services, ApplicationUser user, bool grant = true) =>
        await AdminProvisioning.RunAsync([grant ? "--grant-super-admin" : "--revoke-super-admin", user.Email!], services, TextWriter.Null);
    private async Task<HttpClient> Login(ApplicationUser user)
    {
        var client = app.Browser();
        var html = await client.GetStringAsync("/ingresar");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var result = await client.PostAsync("/cuenta/ingresar", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["email"] = user.Email!, ["password"] = TestPassword, ["__RequestVerificationToken"] = token }));
        Assert.Equal("/eventos", result.Headers.Location?.OriginalString);
        return client;
    }
    private static async Task<AdminService> Service(IServiceProvider services, ApplicationUser user, bool forgeRole = false)
    {
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        if (forgeRole) ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ClaimTypes.Role, AdminAccess.Role));
        return new AdminService(services.GetRequiredService<IDbContextFactory<AppDbContext>>(),
            new AdminAccess(new FixedAuthentication(principal), services.GetRequiredService<IAuthorizationService>()), services.GetRequiredService<IImageStorage>());
    }
    private sealed class FixedAuthentication(ClaimsPrincipal principal) : AuthenticationStateProvider
    { public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(principal)); }

    [Fact]
    public async Task Anonymous_and_regular_accounts_cannot_open_any_administrative_route()
    {
        using var scope = app.Services.CreateScope();
        var user = await Account(scope.ServiceProvider);
        using var anonymous = app.Browser();
        using var regular = await Login(user);
        foreach (var path in new[] { "/administracion", "/administracion/usuarios", "/administracion/eventos", "/administracion/sistema", $"/administracion/eventos/{Guid.NewGuid()}", $"/administracion/media/{Guid.NewGuid()}" })
        {
            var response = await anonymous.GetAsync(path);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Unauthorized });
            response = await regular.GetAsync(path);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Forbidden });
            if (response.StatusCode == HttpStatusCode.Redirect) Assert.Contains("/sin-acceso", response.Headers.Location!.ToString());
            Assert.DoesNotContain("admin-metrics", await response.Content.ReadAsStringAsync());
        }
        Assert.DoesNotContain("href=\"/administracion\"", await regular.GetStringAsync("/eventos"));
        var forged = await Service(scope.ServiceProvider, user, forgeRole: true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => forged.OverviewAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => forged.UsersAsync(null, null, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => forged.UserAsync(user.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => forged.EventsAsync(null, null, null, null, null, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => forged.EventAsync(Guid.NewGuid(), 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => forged.SystemAsync());
    }

    [Fact]
    public async Task Existing_cookie_gains_access_after_grant_and_loses_it_immediately_after_revocation()
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var user = await Account(services);
        using var browser = await Login(user); // Cookie predates the role assignment.
        Assert.Equal(0, await Permission(services, user));
        var admin = await Service(services, user);
        foreach (var path in new[] { "/administracion", "/administracion/usuarios", "/administracion/eventos", "/administracion/sistema" })
        {
            var response = await browser.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.True(response.Headers.CacheControl?.Private);
            Assert.True(response.Headers.CacheControl?.NoTransform);
            Assert.Contains("noindex", response.Headers.GetValues("X-Robots-Tag").Single());
        }
        Assert.Contains("href=\"/administracion\"", await browser.GetStringAsync("/eventos"));
        Assert.Equal(0, await Permission(services, user, grant: false));
        foreach (var path in new[] { "/administracion", "/administracion/usuarios", "/administracion/eventos", "/administracion/sistema", $"/administracion/media/{Guid.NewGuid()}" })
            Assert.Contains((await browser.GetAsync(path)).StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Forbidden });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.OverviewAsync());
    }

    [Fact]
    public async Task Admin_can_inspect_private_gallery_and_responses_without_publishing_them()
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var owner = await Account(services);
        var admin = await Account(services);
        await Permission(services, admin);
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var events = new EventService(factory, new TestUser(owner.Id));
        var item = await events.CreateAsync("Mili y Armando · borrador privado", EventKind.Wedding, DateTime.Today.AddDays(20));
        var gallery = new GalleryService(factory, new TestUser(owner.Id), services.GetRequiredService<IImageStorage>());
        using var bitmap = new SKBitmap(10, 10); bitmap.Erase(SKColors.Pink);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var stream = new MemoryStream(data.ToArray());
        await gallery.AddAsync(item.Id, stream, "Mili y Armando");
        var photo = (await events.GetAsync(item.Id))!.Gallery.Images.Single();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Responses.Add(new GuestResponse { CelebrationId = item.Id, Name = "Mili", Email = "mili@example.test", PartySize = 3, Note = "<script>private-note</script>" });
            await db.SaveChangesAsync();
        }
        using var browser = await Login(admin);
        var html = await browser.GetStringAsync($"/administracion/eventos/{item.Id}");
        Assert.Contains("mili@example.test", html);
        Assert.Contains("&lt;script&gt;private-note&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>private-note</script>", html);
        Assert.DoesNotContain(admin.PasswordHash!, html);
        Assert.DoesNotContain(admin.SecurityStamp!, html);
        var media = await browser.GetAsync($"/administracion/media/{photo.Id}");
        Assert.Equal(HttpStatusCode.OK, media.StatusCode);
        Assert.Equal("image/webp", media.Content.Headers.ContentType?.MediaType);
        Assert.True(media.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/media/{photo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/i/{item.Slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/administracion/eventos/{Guid.NewGuid()}")).StatusCode);
        var info = await (await Service(services, admin)).SystemAsync();
        Assert.Equal(1, info.Files); Assert.Equal(0, info.MissingFiles); Assert.True(info.UploadBytes > 0);
        File.Delete(services.GetRequiredService<IImageStorage>().GetPath(photo.StorageName));
        info = await (await Service(services, admin)).SystemAsync();
        Assert.Equal(1, info.MissingFiles);
        await Permission(services, admin, grant: false);
        Assert.Contains((await browser.GetAsync($"/administracion/media/{photo.Id}")).StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Forbidden });
    }

    [Fact]
    public async Task Statistics_filters_and_pagination_keep_active_and_archived_data_distinct()
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var owner = await Account(services);
        var adminUser = await Account(services);
        await Permission(services, adminUser);
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var events = new EventService(factory, new TestUser(owner.Id));
        var wedding = await events.CreateAsync("Mili y Armando", EventKind.Wedding, DateTime.Today.AddDays(10));
        var birthday = await events.CreateAsync("El cumple de Mili", EventKind.Birthday, DateTime.Today.AddDays(15));
        var archived = await events.CreateAsync("Mili y Armando · archivado", EventKind.Anniversary, DateTime.Today.AddDays(30));
        wedding.IsPublished = true; await events.SaveAsync(wedding, wedding.LocalDate);
        await using (var db = await factory.CreateDbContextAsync())
        {
            (await db.Events.FindAsync(archived.Id))!.IsDeleted = true;
            for (var i = 0; i < 23; i++) db.Responses.Add(new GuestResponse { CelebrationId = wedding.Id, Name = $"Mili {i}", Email = $"mili{i}@example.test", PartySize = 2, Attending = i != 22 });
            db.Responses.Add(new GuestResponse { CelebrationId = archived.Id, Name = "Armando", Email = "armando@example.test", PartySize = 10 });
            await db.SaveChangesAsync();
        }
        var admin = await Service(services, adminUser);
        var overview = await admin.OverviewAsync();
        Assert.Equal(2, overview.Users); Assert.Equal(1, overview.Organizers);
        Assert.Equal(2, overview.Events); Assert.Equal(1, overview.Published); Assert.Equal(1, overview.Drafts); Assert.Equal(1, overview.Archived);
        Assert.Equal(44, overview.Guests); Assert.Equal(23, overview.Responses);
        Assert.Equal(3, overview.RecentEvents); Assert.Equal(3, overview.Trend.Sum(x => x.Count));
        var users = await admin.UsersAsync(owner.Email, "with-events", int.MaxValue);
        Assert.Single(users.Items); Assert.Equal(1, users.Page); Assert.Equal(2, users.Items[0].Events); Assert.Equal(1, users.Items[0].Archived);
        Assert.Single((await admin.UsersAsync(null, "without-events", -1)).Items);
        Assert.Equal(birthday.Id, Assert.Single((await admin.EventsAsync("Mili", "draft", owner.Id, "1", null, -1)).Items).Id);
        Assert.Equal(archived.Id, Assert.Single((await admin.EventsAsync(null, "archived", owner.Id, null, null, 1)).Items).Id);
        Assert.Empty((await admin.EventsAsync(null, "all", adminUser.Id, null, null, 1)).Items);
        var details = (await admin.EventAsync(wedding.Id, -1))!;
        Assert.Equal(20, details.Responses.Items.Count); Assert.Equal(44, details.Guests); Assert.Equal(1, details.Declined);
        details = (await admin.EventAsync(wedding.Id, int.MaxValue))!;
        Assert.Equal(2, details.Responses.Page); Assert.Equal(3, details.Responses.Items.Count);
        using var browser = await Login(adminUser);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/administracion/eventos?usuario={owner.Id}&estado=draft&tipo=1&buscar=Mili")).StatusCode);
        var filtered = await browser.GetStringAsync($"/administracion/eventos?usuario={owner.Id}&estado=archived");
        Assert.Contains(archived.Id.ToString(), filtered); Assert.DoesNotContain(wedding.Id.ToString(), filtered);
    }

    [Fact]
    public async Task Role_provisioning_is_explicit_idempotent_and_never_promotes_the_demo_account()
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var user = await Account(services);
        Assert.Equal(1, await AdminProvisioning.RunAsync(["--grant-super-admin", "absent@example.test"], services, TextWriter.Null));
        Assert.Equal(1, await AdminProvisioning.RunAsync(["--grant-super-admin"], services, TextWriter.Null));
        Assert.Equal(0, await Permission(services, user)); Assert.Equal(0, await Permission(services, user));
        var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var demo = new ApplicationUser { UserName = "demo@savethedate.local", Email = "demo@savethedate.local", DisplayName = "Mili y Armando" };
        Assert.True((await manager.CreateAsync(demo)).Succeeded);
        Assert.Equal(1, await Permission(services, demo));
        Assert.False(await manager.IsInRoleAsync(demo, AdminAccess.Role));
        // A changed security stamp invalidates administrative access before cookie revalidation.
        var admin = await Service(services, user);
        await manager.UpdateSecurityStampAsync(user);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.UsersAsync(null, null, 1));
        Assert.Equal(0, await Permission(services, user, grant: false));
        Assert.Equal(0, await Permission(services, user, grant: false));
        Assert.Null(await manager.FindByEmailAsync("absent@example.test"));
    }
}
