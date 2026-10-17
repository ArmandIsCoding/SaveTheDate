using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SaveTheDate.Data;
using SaveTheDate.Domain;

namespace SaveTheDate.Services;

public class EventService(IDbContextFactory<AppDbContext> factory, ICurrentUser currentUser)
{
    public async Task<List<Celebration>> ListAsync(bool deleted = false)
    {
        var owner = await currentUser.GetIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        return await db.Events.AsNoTracking().Include(x => x.Responses)
            .Where(x => x.IsDeleted == deleted && (x.OwnerId == owner || (!deleted && x.Collaborators.Any(c => c.UserId == owner)))).OrderBy(x => x.StartsAtUtc).ToListAsync();
    }

    public async Task<Celebration?> GetAsync(Guid id)
    {
        var owner = await currentUser.GetIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        return await FullQuery(db).SingleOrDefaultAsync(x => x.Id == id && (x.OwnerId == owner || x.Collaborators.Any(c => c.UserId == owner)) && !x.IsDeleted);
    }

    public static IQueryable<Celebration> FullQuery(AppDbContext db) => db.Events.AsNoTracking().AsSplitQuery()
        .Include(x => x.Modules).Include(x => x.Gallery).ThenInclude(x => x.Images).Include(x => x.Responses);

    public async Task<Celebration> CreateAsync(string title, EventKind kind, DateTime localDate, string zone = "America/Argentina/Cordoba")
    {
        if (title.Trim().Length is < 2 or > 100 || !Enum.IsDefined(kind)) throw new ValidationException("Escribí un nombre de entre 2 y 100 caracteres.");
        var owner = await currentUser.GetIdAsync();
        var item = new Celebration
        {
            OwnerId = owner,
            Title = title.Trim(),
            Kind = kind,
            TimeZoneId = zone,
            StartsAtUtc = ToUtc(localDate, zone),
            Artwork = kind == EventKind.Birthday ? "birthday" : kind == EventKind.Anniversary ? "dinner" : "wedding"
        };
        item.Slug = Slugify(title) + "-" + item.Id.ToString("N")[..8];
        item.Modules = new[] { ModuleKind.Header, ModuleKind.Countdown, ModuleKind.Location, ModuleKind.Gallery, ModuleKind.Rsvp }
            .Select((kind, index) => ModuleCatalog.Create(kind, index)).ToList();
        item.Modules[0].Title = kind == EventKind.Wedding ? "¡Nos casamos!" : "¡Vamos a celebrar!";
        await using var db = await factory.CreateDbContextAsync();
        db.Events.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task SaveAsync(Celebration input, DateTime localDate)
    {
        var owner = await currentUser.GetIdAsync();
        Validator.ValidateObject(input, new ValidationContext(input), true);
        if (input.Title.Trim().Length < 2) throw new ValidationException("El nombre debe tener al menos 2 caracteres.");
        if (!Regex.IsMatch(input.Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$") || input.Slug.Length > 100) throw new ValidationException("El enlace admite letras minúsculas, números y guiones.");
        if (!new[] { "rose", "sage", "lavender" }.Contains(input.Palette)) throw new ValidationException("Elegí una paleta disponible.");
        if (!Enum.IsDefined(input.Kind)) throw new ValidationException("Tipo de evento inválido.");
        if (input.Modules.Count > 30 || input.Modules.Count(x => x.Kind == ModuleKind.Gallery) != 1 || input.Modules.Count(x => x.Kind == ModuleKind.Header) != 1)
            throw new ValidationException("Cada evento necesita una portada y una galería.");
        foreach (var module in input.Modules)
        {
            Validator.ValidateObject(module, new ValidationContext(module), true);
            if (!Enum.IsDefined(module.Kind) || !ModuleCatalog.SafeUrl(module.LinkUrl)) throw new ValidationException("Los enlaces deben comenzar con https://.");
        }
        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.Events.Include(x => x.Modules).SingleOrDefaultAsync(x => x.Id == input.Id && (x.OwnerId == owner || x.Collaborators.Any(c => c.UserId == owner)) && !x.IsDeleted)
            ?? throw new UnauthorizedAccessException();
        if (saved.Version != input.Version) throw new ValidationException("El evento cambió en otra ventana o alguien más guardó cambios. Recargá antes de guardar.");
        if (await db.Events.AnyAsync(x => x.Slug == input.Slug && x.Id != input.Id)) throw new ValidationException("Ese enlace ya está en uso. Elegí otro.");
        saved.Title = input.Title.Trim(); saved.Slug = input.Slug; saved.Kind = input.Kind; saved.Venue = input.Venue.Trim();
        saved.StartsAtUtc = ToUtc(localDate, input.TimeZoneId); saved.TimeZoneId = input.TimeZoneId; saved.Palette = input.Palette;
        saved.IsPublished = input.IsPublished; saved.Version++;
        db.Modules.RemoveRange(saved.Modules);
        saved.Modules = input.Modules.OrderBy(x => x.Position).Select((m, index) => new EventModule
        {
            Kind = m.Kind,
            Position = index,
            Enabled = m.Enabled,
            Title = m.Title,
            Body = m.Body,
            Detail = m.Detail,
            ButtonText = m.ButtonText,
            LinkUrl = m.LinkUrl,
            LocationAddress = m.Kind == ModuleKind.Location ? m.LocationAddress.Trim() : "",
            GooglePlaceId = m.Kind == ModuleKind.Location ? m.GooglePlaceId : ""
        }).ToList();
        db.Modules.AddRange(saved.Modules);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ValidationException("Otro cambio se guardó antes. Recargá el evento."); }
        input.Version = saved.Version;
    }

    public async Task SetDeletedAsync(Guid id, bool deleted)
    {
        var owner = await currentUser.GetIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var item = await db.Events.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner) ?? throw new UnauthorizedAccessException();
        item.IsDeleted = deleted; item.IsPublished = false; item.Version++;
        await db.SaveChangesAsync();
    }

    public static DateTime ToUtc(DateTime local, string zone)
    {
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(zone); }
        catch (TimeZoneNotFoundException) { throw new ValidationException("La zona horaria no es válida."); }
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (local.Year < 2000 || local.Year > 2100 || timeZone.IsInvalidTime(local) || timeZone.IsAmbiguousTime(local)) throw new ValidationException("Revisá la fecha y hora del evento. No puede coincidir con una hora inexistente o repetida por el cambio de horario.");
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    public static string Slugify(string value)
    {
        var normalized = new string(value.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var slug = Regex.Replace(normalized.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "mi-evento" : slug[..Math.Min(slug.Length, 80)].TrimEnd('-');
    }
}
