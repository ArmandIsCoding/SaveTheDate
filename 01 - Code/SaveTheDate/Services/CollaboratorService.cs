using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaveTheDate.Data;
using SaveTheDate.Domain;

namespace SaveTheDate.Services;

public record CollaboratorRow(string UserId, string Name, string Email);

public class CollaboratorService(IDbContextFactory<AppDbContext> factory, ICurrentUser currentUser, ILookupNormalizer normalizer)
{
    private async Task RequireOwnerAsync(AppDbContext db, Guid eventId)
    {
        var userId = await currentUser.GetIdAsync();
        if (!await db.Events.AnyAsync(e => e.Id == eventId && e.OwnerId == userId && !e.IsDeleted))
            throw new UnauthorizedAccessException();
    }

    public async Task<List<CollaboratorRow>> ListAsync(Guid eventId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await RequireOwnerAsync(db, eventId);
        return await db.Collaborators.Where(c => c.CelebrationId == eventId).OrderBy(c => c.AddedAtUtc)
            .Select(c => new CollaboratorRow(c.UserId, c.User.DisplayName, c.User.Email ?? "")).ToListAsync();
    }

    public async Task AddAsync(Guid eventId, string email)
    {
        await using var db = await factory.CreateDbContextAsync();
        await RequireOwnerAsync(db, eventId);
        email = email.Trim();
        if (email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
            throw new ValidationException("Ingresá un correo válido.");
        var normalized = normalizer.NormalizeEmail(email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized)
            ?? throw new ValidationException("No encontramos una cuenta con ese correo. Pedile que se registre primero.");
        if (user.Id == await currentUser.GetIdAsync())
            throw new ValidationException("Ya sos el creador de este evento.");
        if (await db.Collaborators.AnyAsync(c => c.CelebrationId == eventId && c.UserId == user.Id))
            throw new ValidationException("Esta persona ya colabora en el evento.");
        db.Collaborators.Add(new EventCollaborator { CelebrationId = eventId, UserId = user.Id });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 })
        { throw new ValidationException("Esta persona ya colabora en el evento."); }
    }

    public async Task RemoveAsync(Guid eventId, string userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await RequireOwnerAsync(db, eventId);
        await db.Collaborators.Where(c => c.CelebrationId == eventId && c.UserId == userId).ExecuteDeleteAsync();
    }
}
