using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SaveTheDate.Data;
using SaveTheDate.Domain;

namespace SaveTheDate.Services;

public class InvitationService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<Celebration?> GetAsync(string slug)
    {
        await using var db = await factory.CreateDbContextAsync();
        // Public queries deliberately never include guest contact information.
        return await db.Events.AsNoTracking().AsSplitQuery().Include(x => x.Modules).Include(x => x.Gallery).ThenInclude(x => x.Images)
            .SingleOrDefaultAsync(x => x.Slug == slug && x.IsPublished && !x.IsDeleted);
    }

    public async Task RespondAsync(string slug, GuestResponse response)
    {
        response.Name = response.Name.Trim(); response.Email = response.Email.Trim().ToLowerInvariant();
        Validator.ValidateObject(response, new ValidationContext(response), true);
        await using var db = await factory.CreateDbContextAsync();
        var item = await db.Events.SingleOrDefaultAsync(x => x.Slug == slug && x.IsPublished && !x.IsDeleted && x.Modules.Any(m => m.Kind == ModuleKind.Rsvp && m.Enabled))
            ?? throw new ValidationException("Esta invitación no está recibiendo respuestas.");
        if (await db.Responses.AnyAsync(x => x.CelebrationId == item.Id && x.Email == response.Email))
            throw new ValidationException("Ya recibimos una respuesta con ese correo. Contactá a quien organiza para cambiarla.");
        response.CelebrationId = item.Id;
        if (!response.Attending) response.PartySize = 1;
        db.Responses.Add(response);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new ValidationException("No pudimos guardar la respuesta. Puede que ya haya sido enviada."); }
    }
}
