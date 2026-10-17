using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SaveTheDate.Domain;

namespace SaveTheDate.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Celebration> Events => Set<Celebration>();
    public DbSet<EventModule> Modules => Set<EventModule>();
    public DbSet<EventGallery> Galleries => Set<EventGallery>();
    public DbSet<GalleryImage> Images => Set<GalleryImage>();
    public DbSet<GuestResponse> Responses => Set<GuestResponse>();
    public DbSet<EventCollaborator> Collaborators => Set<EventCollaborator>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<EventCollaborator>().HasKey(x => new { x.CelebrationId, x.UserId });
        builder.Entity<Celebration>().HasMany(x => x.Collaborators).WithOne().HasForeignKey(x => x.CelebrationId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EventCollaborator>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Celebration>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<Celebration>().HasIndex(x => new { x.OwnerId, x.IsDeleted });
        builder.Entity<Celebration>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Celebration>().HasMany(x => x.Modules).WithOne().HasForeignKey(x => x.CelebrationId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Celebration>().HasOne(x => x.Gallery).WithOne().HasForeignKey<EventGallery>(x => x.CelebrationId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EventGallery>().HasMany(x => x.Images).WithOne().HasForeignKey(x => x.EventGalleryId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Celebration>().HasMany(x => x.Responses).WithOne().HasForeignKey(x => x.CelebrationId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<GuestResponse>().HasIndex(x => new { x.CelebrationId, x.Email }).IsUnique();
        builder.Entity<Celebration>().Ignore(x => x.LocalDate).Ignore(x => x.CoverUrl);
    }
}
