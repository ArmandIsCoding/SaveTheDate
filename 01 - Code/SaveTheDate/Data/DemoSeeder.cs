using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaveTheDate.Domain;
using SaveTheDate.Services;

namespace SaveTheDate.Data;

public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var user = await users.FindByEmailAsync("demo@savethedate.local");
        if (user is not null) return;
        user = new ApplicationUser { UserName = "demo@savethedate.local", Email = "demo@savethedate.local", DisplayName = "Mili y Armando" };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded) throw new InvalidOperationException("No se pudo crear el espacio demo.");
        var titles = new[] { "Mili & Armando", "El cumple de Mili", "Mili y Armando · Una vida juntos" };
        var art = new[] { "wedding", "birthday", "dinner" };
        for (var i = 0; i < 3; i++)
        {
            var item = new Celebration
            {
                OwnerId = user.Id,
                Title = titles[i],
                Slug = new[] { "mili-y-armando", "cumple-de-mili", "mili-y-armando-aniversario" }[i],
                Kind = (EventKind)i,
                StartsAtUtc = EventService.ToUtc(DateTime.Today.AddDays(45 + i * 32).AddHours(18), "America/Argentina/Cordoba"),
                Venue = new[] { "Estancia La Paz, Córdoba", "Casa de Mili y Armando, Córdoba", "Jardín de los Olivos, Córdoba" }[i],
                Artwork = art[i],
                Palette = new[] { "rose", "lavender", "sage" }[i],
                IsPublished = i == 0
            };
            item.Modules = new[] { ModuleKind.Header, ModuleKind.Countdown, ModuleKind.Story, ModuleKind.Location, ModuleKind.Gallery, ModuleKind.DressCode, ModuleKind.Rsvp }
                .Select((kind, position) => ModuleCatalog.Create(kind, position)).ToList();
            item.Modules[0].Title = i == 0 ? "¡Nos casamos!" : "La vida merece celebrarse";
            item.Modules[0].Body = "Hay momentos que se vuelven eternos. Queremos compartir este con vos.";
            item.Modules[2].Body = "Un encuentro inesperado, miles de charlas y una misma forma de mirar el mundo. Hoy elegimos seguir escribiendo nuestra historia, rodeados de las personas que más queremos.";
            item.Modules[3].Body = item.Venue; item.Modules[3].Detail = "Te esperamos para una tarde inolvidable.";
            item.Modules[3].LinkUrl = "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(item.Venue);
            item.Modules[3].ButtonText = "Cómo llegar";
            item.Modules[5].Body = "Elegante, cómodo y con ganas de bailar.";
            var environment = services.GetRequiredService<IWebHostEnvironment>();
            var storage = services.GetRequiredService<IImageStorage>();
            foreach (var (imageName, caption) in new[] { ("wedding", "Mili y Armando · El comienzo de algo hermoso"), ("dinner", "Mili y Armando · Un brindis por lo que viene"), ("birthday", "Mili y Armando · La parte más dulce") })
            {
                await using var stream = File.OpenRead(Path.Combine(environment.WebRootPath, "images", imageName + ".jpg"));
                var name = await storage.SaveAsync(stream);
                item.Gallery.Images.Add(new GalleryImage { StorageName = name, Caption = caption, Position = item.Gallery.Images.Count });
            }
            db.Events.Add(item);
        }
        await db.SaveChangesAsync();
    }
}
