using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SaveTheDate.Domain;

public class ApplicationUser : IdentityUser
{
    [MaxLength(80)] public string DisplayName { get; set; } = "";
}

public enum EventKind { Wedding, Birthday, Anniversary, Other }
public enum ModuleKind { Header, Countdown, Story, Location, Gallery, DressCode, Gifts, Rsvp, Music, Information, Social }

public class Celebration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(450)] public string OwnerId { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 2)] public string Title { get; set; } = "";
    [Required, MaxLength(100)] public string Slug { get; set; } = "";
    public EventKind Kind { get; set; }
    public DateTime StartsAtUtc { get; set; }
    [MaxLength(100)] public string TimeZoneId { get; set; } = "America/Argentina/Cordoba";
    [MaxLength(160)] public string Venue { get; set; } = "";
    [MaxLength(20)] public string Palette { get; set; } = "rose";
    [MaxLength(20)] public string Artwork { get; set; } = "wedding";
    public Guid? CoverImageId { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [ConcurrencyCheck] public int Version { get; set; }
    public List<EventModule> Modules { get; set; } = [];
    public EventGallery Gallery { get; set; } = new();
    public List<GuestResponse> Responses { get; set; } = [];

    public DateTime LocalDate => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(StartsAtUtc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
    public string CoverUrl => CoverImageId.HasValue ? $"/media/{CoverImageId}" : $"/images/{Artwork}.jpg";
}

public class EventModule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CelebrationId { get; set; }
    public ModuleKind Kind { get; set; }
    public int Position { get; set; }
    public bool Enabled { get; set; } = true;
    [MaxLength(120)] public string Title { get; set; } = "";
    [MaxLength(4000)] public string Body { get; set; } = "";
    [MaxLength(500)] public string Detail { get; set; } = "";
    [MaxLength(80)] public string ButtonText { get; set; } = "";
    [MaxLength(1000)] public string LinkUrl { get; set; } = "";
    [MaxLength(500)] public string LocationAddress { get; set; } = "";
    [MaxLength(500), RegularExpression(@"^[A-Za-z0-9_-]*$", ErrorMessage = "Volvé a elegir el lugar en Google Maps.")]
    public string GooglePlaceId { get; set; } = "";
}

public class EventGallery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CelebrationId { get; set; }
    public List<GalleryImage> Images { get; set; } = [];
}

public class GalleryImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventGalleryId { get; set; }
    [MaxLength(80)] public string StorageName { get; set; } = "";
    [MaxLength(180)] public string Caption { get; set; } = "";
    public int Position { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class GuestResponse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CelebrationId { get; set; }
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, EmailAddress, MaxLength(200)] public string Email { get; set; } = "";
    public bool Attending { get; set; } = true;
    [Range(1, 10)] public int PartySize { get; set; } = 1;
    [MaxLength(500)] public string Note { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
