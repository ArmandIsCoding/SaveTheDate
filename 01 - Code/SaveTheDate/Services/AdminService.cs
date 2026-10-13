using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SaveTheDate.Data;
using SaveTheDate.Domain;

namespace SaveTheDate.Services;

public record AdminPage<T>(List<T> Items, int Total, int Page, int PageSize)
{
    public int Pages => Math.Max(1, (int)Math.Ceiling((double)Total / PageSize));
}
public record AdminUserRow(string Id, string Name, string Email, int Events, int Published, int Drafts, int Archived, bool SuperAdmin, DateTimeOffset? LockoutEnd);
public record AdminEventRow(Guid Id, string Title, string Slug, string OwnerId, string OwnerName, string OwnerEmail,
    EventKind Kind, DateTime StartsAtUtc, string TimeZoneId, DateTime CreatedAtUtc, bool Published, bool Deleted, int Photos, int Responses, int Guests);
public record AdminTrend(string Label, int Count);
public record AdminCategory(EventKind Kind, int Count);
public record AdminOverview(int Users, int Organizers, int Events, int Published, int Drafts, int Archived,
    int Photos, int Responses, int Guests, int RecentEvents, int UpcomingEvents, List<AdminTrend> Trend,
    List<AdminCategory> Categories, List<AdminEventRow> Upcoming, List<AdminEventRow> Recent, DateTime CapturedAtUtc);
public record AdminEventDetail(Celebration Event, string OwnerName, string OwnerEmail, int Guests, int Declined, AdminPage<GuestResponse> Responses);
public record AdminSystemInfo(long? DatabaseBytes, long? UploadBytes, int Files, int MissingFiles, bool FilesReadable, string Runtime);

public class AdminService(IDbContextFactory<AppDbContext> factory, AdminAccess access, IImageStorage storage)
{
    public const int PageSize = 20;
    private static readonly Expression<Func<Celebration, AdminEventRow>> EventProjection = e => new(
        e.Id, e.Title, e.Slug, e.OwnerId, "", "", e.Kind, e.StartsAtUtc, e.TimeZoneId, e.CreatedAtUtc,
        e.IsPublished, e.IsDeleted, e.Gallery.Images.Count, e.Responses.Count,
        e.Responses.Where(r => r.Attending).Sum(r => r.PartySize));

    public async Task<AdminOverview> OverviewAsync()
    {
        await access.RequireAsync();
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var active = db.Events.AsNoTracking().Where(e => !e.IsDeleted);
        var users = await db.Users.CountAsync();
        var organizers = await db.Users.CountAsync(u => active.Any(e => e.OwnerId == u.Id));
        var events = await active.CountAsync();
        var published = await active.CountAsync(e => e.IsPublished);
        var archived = await db.Events.CountAsync(e => e.IsDeleted);
        var photos = await db.Images.CountAsync();
        var responses = await db.Responses.CountAsync(r => active.Any(e => e.Id == r.CelebrationId));
        var guests = await db.Responses.Where(r => r.Attending && active.Any(e => e.Id == r.CelebrationId)).SumAsync(r => (int?)r.PartySize) ?? 0;
        var recentCount = await db.Events.CountAsync(e => e.CreatedAtUtc >= now.AddDays(-30));
        var upcomingCount = await active.CountAsync(e => e.StartsAtUtc >= now && e.StartsAtUtc < now.AddDays(30));
        var categories = await active.GroupBy(e => e.Kind).OrderByDescending(g => g.Count()).Select(g => new AdminCategory(g.Key, g.Count())).ToListAsync();
        var recent = await WithOwnersAsync(db, active.OrderByDescending(e => e.CreatedAtUtc).ThenBy(e => e.Id).Take(5));
        var upcoming = await WithOwnersAsync(db, active.Where(e => e.StartsAtUtc >= now).OrderBy(e => e.StartsAtUtc).ThenBy(e => e.Id).Take(5));
        var local = AdminFormat.Local(now);
        var month = new DateTime(local.Year, local.Month, 1).AddMonths(-5);
        var trend = new List<AdminTrend>();
        for (var i = 0; i < 6; i++)
        {
            var start = month.AddMonths(i);
            var from = EventService.ToUtc(start, AdminFormat.Zone);
            var until = EventService.ToUtc(start.AddMonths(1), AdminFormat.Zone);
            trend.Add(new AdminTrend(start.ToString("MMM", CultureInfo.GetCultureInfo("es-AR")), await db.Events.CountAsync(e => e.CreatedAtUtc >= from && e.CreatedAtUtc < until)));
        }
        return new(users, organizers, events, published, events - published, archived, photos, responses, guests, recentCount, upcomingCount, trend, categories, upcoming, recent, now);
    }

    public async Task<AdminPage<AdminUserRow>> UsersAsync(string? search, string? filter, int page)
    {
        await access.RequireAsync();
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Users.AsNoTracking();
        var term = SearchTerm(search);
        if (term.Length > 0) query = query.Where(u => u.DisplayName.ToUpper().Contains(term) || (u.NormalizedEmail != null && u.NormalizedEmail.Contains(term)));
        if (filter == "with-events") query = query.Where(u => db.Events.Any(e => e.OwnerId == u.Id && !e.IsDeleted));
        if (filter == "without-events") query = query.Where(u => !db.Events.Any(e => e.OwnerId == u.Id && !e.IsDeleted));
        var total = await query.CountAsync();
        page = ClampPage(page, total);
        var rows = await query.OrderBy(u => u.DisplayName).ThenBy(u => u.Id).Skip((page - 1) * PageSize).Take(PageSize)
            .Select(u => new AdminUserRow(u.Id, u.DisplayName, u.Email ?? "", db.Events.Count(e => e.OwnerId == u.Id && !e.IsDeleted),
                db.Events.Count(e => e.OwnerId == u.Id && !e.IsDeleted && e.IsPublished),
                db.Events.Count(e => e.OwnerId == u.Id && !e.IsDeleted && !e.IsPublished), db.Events.Count(e => e.OwnerId == u.Id && e.IsDeleted),
                db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AdminAccess.Role)), u.LockoutEnd)).ToListAsync();
        return new(rows, total, page, PageSize);
    }

    public async Task<AdminUserRow?> UserAsync(string id)
    {
        await access.RequireAsync();
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => new AdminUserRow(u.Id, u.DisplayName, u.Email ?? "",
            db.Events.Count(e => e.OwnerId == u.Id && !e.IsDeleted), db.Events.Count(e => e.OwnerId == u.Id && !e.IsDeleted && e.IsPublished),
            db.Events.Count(e => e.OwnerId == u.Id && !e.IsDeleted && !e.IsPublished), db.Events.Count(e => e.OwnerId == u.Id && e.IsDeleted),
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AdminAccess.Role)), u.LockoutEnd)).SingleOrDefaultAsync();
    }

    public async Task<AdminPage<AdminEventRow>> EventsAsync(string? search, string? status, string? owner, string? kind, string? order, int page)
    {
        await access.RequireAsync();
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Events.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(owner)) query = query.Where(e => e.OwnerId == owner);
        query = status switch
        {
            "published" => query.Where(e => !e.IsDeleted && e.IsPublished),
            "draft" => query.Where(e => !e.IsDeleted && !e.IsPublished),
            "archived" => query.Where(e => e.IsDeleted),
            "all" => query,
            _ => query.Where(e => !e.IsDeleted)
        };
        if (int.TryParse(kind, out var type) && Enum.IsDefined(typeof(EventKind), type)) query = query.Where(e => e.Kind == (EventKind)type);
        var term = SearchTerm(search);
        if (term.Length > 0) query = query.Where(e => e.Title.ToUpper().Contains(term) || e.Slug.ToUpper().Contains(term)
            || db.Users.Any(u => u.Id == e.OwnerId && (u.DisplayName.ToUpper().Contains(term) || (u.NormalizedEmail != null && u.NormalizedEmail.Contains(term)))));
        if (order == "upcoming") query = query.Where(e => e.StartsAtUtc >= DateTime.UtcNow);
        var total = await query.CountAsync();
        page = ClampPage(page, total);
        var sorted = order == "upcoming" ? query.OrderBy(e => e.StartsAtUtc).ThenBy(e => e.Id) : query.OrderByDescending(e => e.CreatedAtUtc).ThenBy(e => e.Id);
        return new(await WithOwnersAsync(db, sorted.Skip((page - 1) * PageSize).Take(PageSize)), total, page, PageSize);
    }

    public async Task<AdminEventDetail?> EventAsync(Guid id, int page)
    {
        await access.RequireAsync();
        await using var db = await factory.CreateDbContextAsync();
        var item = await db.Events.AsNoTracking().Include(e => e.Modules).Include(e => e.Gallery).ThenInclude(g => g.Images)
            .AsSplitQuery().SingleOrDefaultAsync(e => e.Id == id);
        if (item is null) return null;
        var owner = await db.Users.Where(u => u.Id == item.OwnerId).Select(u => new { u.DisplayName, u.Email }).SingleAsync();
        var query = db.Responses.AsNoTracking().Where(r => r.CelebrationId == id);
        var total = await query.CountAsync();
        page = ClampPage(page, total);
        var rows = await query.OrderByDescending(r => r.UpdatedAtUtc).ThenBy(r => r.Id).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        var guests = await query.Where(r => r.Attending).SumAsync(r => (int?)r.PartySize) ?? 0;
        var declined = await query.CountAsync(r => !r.Attending);
        return new(item, owner.DisplayName, owner.Email ?? "", guests, declined, new(rows, total, page, PageSize));
    }

    public async Task<AdminSystemInfo> SystemAsync()
    {
        await access.RequireAsync();
        await using var db = await factory.CreateDbContextAsync();
        var readable = true;
        long? databaseBytes = null;
        try
        {
            var dataSource = db.Database.GetDbConnection().DataSource;
            if (File.Exists(dataSource))
                databaseBytes = new[] { dataSource, dataSource + "-wal", dataSource + "-shm" }.Where(File.Exists).Sum(p => new FileInfo(p).Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { readable = false; }
        long bytes = 0;
        var files = 0;
        var missing = 0;
        // Stream the names instead of loading every image/entity in memory.
        await foreach (var name in db.Images.AsNoTracking().Select(p => p.StorageName).AsAsyncEnumerable())
        {
            try
            {
                var file = new FileInfo(storage.GetPath(name));
                if (file.Exists) { bytes += file.Length; files++; }
                else missing++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { readable = false; }
        }
        return new(databaseBytes, readable ? bytes : null, files, missing, readable, Environment.Version.ToString());
    }

    private static async Task<List<AdminEventRow>> WithOwnersAsync(AppDbContext db, IQueryable<Celebration> query)
    {
        var rows = await query.Select(EventProjection).ToListAsync();
        var ids = rows.Select(e => e.OwnerId).Distinct().ToArray();
        var owners = await db.Users.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName, u.Email }).ToDictionaryAsync(u => u.Id);
        return rows.Select(e => e with { OwnerName = owners[e.OwnerId].DisplayName, OwnerEmail = owners[e.OwnerId].Email ?? "" }).ToList();
    }
    private static int ClampPage(int page, int total) => Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)total / PageSize)));
    private static string SearchTerm(string? value)
    {
        var term = (value ?? "").Trim();
        return term[..Math.Min(term.Length, 200)].ToUpperInvariant();
    }
}

public static class AdminFormat
{
    public const string Zone = "America/Argentina/Cordoba";
    public static DateTime Local(DateTime utc, string zone = Zone) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(zone));
    public static string Size(long? bytes) => bytes is null ? "No disponible" : bytes < 1024 * 1024 ? $"{bytes / 1024d:N1} KB" : bytes < 1024L * 1024 * 1024 ? $"{bytes / (1024d * 1024):N1} MB" : $"{bytes / (1024d * 1024 * 1024):N1} GB";
    public static string Status(bool published, bool deleted) => deleted ? "En papelera" : published ? "Publicado" : "Borrador";
    public static string StatusClass(bool published, bool deleted) => deleted ? "archived" : published ? "published" : "draft";
    public static string Initials(string name) => string.IsNullOrWhiteSpace(name) ? "?" : string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0])).ToUpperInvariant();
}
