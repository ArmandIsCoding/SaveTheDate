using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SaveTheDate.Domain;
using SaveTheDate.Services;
using SkiaSharp;

namespace SaveTheDate.Tests;

public sealed class EventAndGalleryTests : IAsyncLifetime
{
    readonly string root = Path.Combine(Path.GetTempPath(), "savethedate-test-" + Guid.NewGuid());
    readonly TestUser current = new();
    TestDbFactory factory = default!;
    EventService events = default!;
    GalleryService gallery = default!;
    LocalImageStorage storage = default!;
    InvitationService invitations = default!;
    readonly DateTime localDate = new(2027, 2, 20, 18, 30, 0);

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(root);
        factory = new TestDbFactory(Path.Combine(root, "test.db"));
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(new ApplicationUser { Id = "owner", UserName = "one@example.test" }, new ApplicationUser { Id = "other", UserName = "two@example.test" });
        await db.SaveChangesAsync();
        events = new EventService(factory, current);
        storage = new LocalImageStorage(new TestEnvironment(root));
        gallery = new GalleryService(factory, current, storage);
        invitations = new InvitationService(factory);
    }

    public Task DisposeAsync() { Directory.Delete(root, true); return Task.CompletedTask; }
    Task<Celebration> Create() => events.CreateAsync("Lucía & Tomás", EventKind.Wedding, localDate);
    static MemoryStream Photo()
    {
        using var bitmap = new SKBitmap(60, 40);
        bitmap.Erase(SKColors.LightPink);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(encoded.ToArray());
    }

    [Fact]
    public async Task Creation_persists_draft_with_independent_gallery_and_correct_utc_time()
    {
        var first = await Create(); var second = await Create();
        var read = (await events.GetAsync(first.Id))!;
        Assert.False(read.IsPublished);
        Assert.NotEqual(first.Gallery.Id, second.Gallery.Id);
        Assert.Contains(read.Modules, x => x.Kind == ModuleKind.Gallery);
        Assert.Equal(new DateTime(2027, 2, 20, 21, 30, 0), read.StartsAtUtc);
        Assert.Equal(localDate, read.LocalDate);
        Assert.StartsWith("lucia-tomas-", read.Slug);
        Assert.NotEqual(first.Slug, second.Slug);
    }

    [Fact]
    public async Task Owners_cannot_read_change_or_upload_to_another_users_event()
    {
        var item = await Create();
        current.Id = "other";
        Assert.Empty(await events.ListAsync());
        Assert.Null(await events.GetAsync(item.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => events.SaveAsync(item, localDate));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => events.SetDeletedAsync(item.Id, true));
        using var photo = Photo();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => gallery.AddAsync(item.Id, photo, "Private"));
    }

    [Fact]
    public async Task Module_edits_order_and_visibility_survive_two_saves()
    {
        var item = await Create();
        item.Modules.Add(ModuleCatalog.Create(ModuleKind.Gifts, 5));
        item.Modules[0].Body = "Nuestra historia";
        item.Modules[1].Enabled = false;
        await events.SaveAsync(item, localDate);
        item.Title = "Un nuevo nombre";
        await events.SaveAsync(item, localDate);
        var read = (await events.GetAsync(item.Id))!;
        Assert.Equal("Un nuevo nombre", read.Title);
        Assert.Equal(6, read.Modules.Count);
        Assert.False(read.Modules.Single(x => x.Kind == ModuleKind.Countdown).Enabled);
        Assert.Equal("Nuestra historia", read.Modules.Single(x => x.Kind == ModuleKind.Header).Body);
        Assert.Equal(5, read.Modules.Single(x => x.Kind == ModuleKind.Gifts).Position);
    }

    [Fact]
    public async Task Stale_editor_cannot_overwrite_a_newer_save()
    {
        var item = await Create(); var old = (await events.GetAsync(item.Id))!;
        item.Title = "Más reciente";
        await events.SaveAsync(item, localDate);
        await Assert.ThrowsAsync<ValidationException>(() => events.SaveAsync(old, localDate));
        Assert.Equal("Más reciente", (await events.GetAsync(item.Id))!.Title);
    }

    [Fact]
    public async Task Unsafe_links_and_missing_required_modules_are_rejected()
    {
        var item = await Create(); item.Modules[2].LinkUrl = "javascript:alert(1)";
        await Assert.ThrowsAsync<ValidationException>(() => events.SaveAsync(item, localDate));
        item.Modules[2].LinkUrl = "https://example.com/";
        item.Modules.RemoveAll(x => x.Kind == ModuleKind.Gallery);
        await Assert.ThrowsAsync<ValidationException>(() => events.SaveAsync(item, localDate));
    }

    [Fact]
    public async Task Only_published_active_events_are_public_and_restore_returns_to_draft()
    {
        var item = await Create(); Assert.Null(await invitations.GetAsync(item.Slug));
        item.IsPublished = true; await events.SaveAsync(item, localDate);
        Assert.NotNull(await invitations.GetAsync(item.Slug));
        await events.SetDeletedAsync(item.Id, true);
        Assert.Null(await invitations.GetAsync(item.Slug)); Assert.Null(await events.GetAsync(item.Id));
        Assert.Single(await events.ListAsync(true));
        await events.SetDeletedAsync(item.Id, false);
        Assert.False((await events.GetAsync(item.Id))!.IsPublished);
    }

    [Fact]
    public async Task Gallery_upload_update_reorder_cover_and_delete_persist_and_remove_files()
    {
        var item = await Create();
        using (var photo = Photo()) await gallery.AddAsync(item.Id, photo, "Primera");
        using (var photo = Photo()) await gallery.AddAsync(item.Id, photo, "Segunda");
        var read = (await events.GetAsync(item.Id))!;
        var second = read.Gallery.Images.Single(x => x.Caption == "Segunda");
        Assert.True(File.Exists(storage.GetPath(second.StorageName)));
        using (var codec = SKCodec.Create(storage.GetPath(second.StorageName))) Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        await gallery.UpdateAsync(item.Id, second.Id, "Otro recuerdo", -1);
        await gallery.SetCoverAsync(item.Id, second.Id);
        read = (await events.GetAsync(item.Id))!;
        Assert.Equal(second.Id, read.CoverImageId);
        Assert.Equal("Otro recuerdo", read.Gallery.Images.Single(x => x.Position == 0).Caption);
        await gallery.DeleteAsync(item.Id, second.Id);
        Assert.False(File.Exists(storage.GetPath(second.StorageName)));
        read = (await events.GetAsync(item.Id))!;
        Assert.Null(read.CoverImageId); Assert.Single(read.Gallery.Images);
        Assert.Equal(0, read.Gallery.Images.Single().Position);
        // Gallery operations do not invalidate unrelated pending configuration edits.
        item.Title = "Cambios pendientes";
        await events.SaveAsync(item, localDate);
        Assert.Equal("Cambios pendientes", (await events.GetAsync(item.Id))!.Title);
    }

    [Fact]
    public async Task Gallery_rejects_cross_event_image_ids_and_non_image_payloads()
    {
        var first = await Create(); var second = await Create();
        using (var photo = Photo()) await gallery.AddAsync(first.Id, photo, "Privada");
        var id = (await events.GetAsync(first.Id))!.Gallery.Images.Single().Id;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => gallery.DeleteAsync(second.Id, id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => gallery.SetCoverAsync(second.Id, id));
        using var script = new MemoryStream(Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"));
        await Assert.ThrowsAsync<ValidationException>(() => gallery.AddAsync(second.Id, script, "fake.jpg"));
        using var huge = new MemoryStream(new byte[LocalImageStorage.MaxBytes + 1]);
        await Assert.ThrowsAsync<ValidationException>(() => gallery.AddAsync(second.Id, huge, "too-large.jpg"));
    }

    [Fact]
    public async Task Guest_responses_are_validated_private_and_cannot_be_overwritten_by_email()
    {
        var item = await Create();
        GuestResponse Response() => new() { Name = "Invitado", Email = " PERSON@example.test ", PartySize = 2 };
        await Assert.ThrowsAsync<ValidationException>(() => invitations.RespondAsync(item.Slug, Response()));
        item.IsPublished = true; await events.SaveAsync(item, localDate);
        await invitations.RespondAsync(item.Slug, Response());
        await Assert.ThrowsAsync<ValidationException>(() => invitations.RespondAsync(item.Slug, Response()));
        Assert.Empty((await invitations.GetAsync(item.Slug))!.Responses);
        Assert.Equal(2, (await events.GetAsync(item.Id))!.Responses.Single().PartySize);
        var invalid = new GuestResponse { Name = "X", Email = "bad", PartySize = 25 };
        await Assert.ThrowsAsync<ValidationException>(() => invitations.RespondAsync(item.Slug, invalid));
        item.Modules.Single(x => x.Kind == ModuleKind.Rsvp).Enabled = false;
        await events.SaveAsync(item, localDate);
        await Assert.ThrowsAsync<ValidationException>(() => invitations.RespondAsync(item.Slug, new GuestResponse { Name = "Otro", Email = "other@example.test" }));
    }

    [Fact]
    public async Task Calendar_escapes_content_and_uses_utc()
    {
        var item = await Create(); item.Title = "Hola; mundo\nEND:VEVENT";
        var text = Encoding.UTF8.GetString(CalendarFile.Create(item));
        Assert.Contains("DTSTART:20270220T213000Z", text);
        Assert.Contains("SUMMARY:Hola\\; mundo\\nEND:VEVENT", text);
        Assert.Equal(1, text.Split("\r\nEND:VEVENT\r\n").Length - 1);
    }
}
