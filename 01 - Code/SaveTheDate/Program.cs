using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaveTheDate.Components;
using SaveTheDate.Data;
using SaveTheDate.Domain;
using SaveTheDate.Endpoints;
using SaveTheDate.Services;

var builder = WebApplication.CreateBuilder(args);
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("es-AR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityProvider>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.AddAuthorization();
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
}).AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/ingresar";
    options.AccessDeniedPath = "/ingresar";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});
var dataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataPath);
var connection = builder.Configuration.GetConnectionString("DefaultConnection") ?? $"Data Source={Path.Combine(dataPath, "savethedate.db")}";
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connection));
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<GalleryService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddSingleton<IImageStorage, LocalImageStorage>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    foreach (var name in new[] { "accounts", "rsvp" })
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    try { await next(context); }
    catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsync("La sesión del formulario venció. Recargá la página."); }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapAccountEndpoints();
app.MapGet("/i/{slug}/calendario", async (string slug, InvitationService invitations) =>
{
    var item = await invitations.GetAsync(slug);
    return item is null ? Results.NotFound() : Results.File(CalendarFile.Create(item), "text/calendar; charset=utf-8", "save-the-date.ics");
});
app.MapGet("/media/{id:guid}", async (Guid id, HttpContext context, IDbContextFactory<AppDbContext> factory, IImageStorage storage) =>
{
    await using var db = await factory.CreateDbContextAsync();
    var owner = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var photo = await db.Images.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && db.Events.Any(e => e.Gallery.Id == x.EventGalleryId && !e.IsDeleted && (e.IsPublished || e.OwnerId == owner)));
    if (photo is null || !File.Exists(storage.GetPath(photo.StorageName))) return Results.NotFound();
    context.Response.Headers.CacheControl = "private, no-store";
    return Results.File(storage.GetPath(photo.StorageName), "image/webp");
});
app.MapPost("/i/{slug}/confirmar", async (string slug, HttpContext context, IAntiforgery csrf, InvitationService invitations) =>
{
    await csrf.ValidateRequestAsync(context);
    var form = await context.Request.ReadFormAsync();
    if (!string.IsNullOrEmpty(form["website"])) return Results.BadRequest();
    try
    {
        await invitations.RespondAsync(slug, new GuestResponse
        {
            Name = form["name"].ToString(),
            Email = form["email"].ToString(),
            Attending = form["attending"] == "yes",
            PartySize = int.TryParse(form["size"], out var count) ? count : 0,
            Note = form["note"].ToString()
        });
        return Results.LocalRedirect($"/i/{Uri.EscapeDataString(slug)}?respuesta=ok#rsvp");
    }
    catch (ValidationException exception) { return Results.LocalRedirect($"/i/{Uri.EscapeDataString(slug)}?error={Uri.EscapeDataString(exception.Message)}#rsvp"); }
}).RequireRateLimiting("rsvp");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:ApplyMigrations")) await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment()) await DemoSeeder.SeedAsync(scope.ServiceProvider);
}
app.Run();
public partial class Program { }
