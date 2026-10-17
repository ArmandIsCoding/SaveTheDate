using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SaveTheDate.Data;
using SaveTheDate.Domain;
using SkiaSharp;

namespace SaveTheDate.Services;

public interface IImageStorage
{
    Task<string> SaveAsync(Stream input);
    string GetPath(string name);
    void Delete(string name);
}

public class LocalImageStorage(IWebHostEnvironment environment) : IImageStorage
{
    public const long MaxBytes = 10 * 1024 * 1024;
    private readonly string root = Path.Combine(environment.ContentRootPath, "App_Data", "uploads");
    public string GetPath(string name) => Path.Combine(root, Path.GetFileName(name));
    public void Delete(string name) { var path = GetPath(name); if (File.Exists(path)) File.Delete(path); }

    public async Task<string> SaveAsync(Stream input)
    {
        using var buffer = new MemoryStream();
        var bytes = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(bytes)) > 0)
        {
            if (buffer.Length + read > MaxBytes) throw new ValidationException("Cada imagen puede pesar hasta 10 MB.");
            await buffer.WriteAsync(bytes.AsMemory(0, read));
        }
        buffer.Position = 0;
        using var codec = SKCodec.Create(buffer);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            throw new ValidationException("Usá una foto JPG, PNG o WebP válida.");
        if ((long)codec.Info.Width * codec.Info.Height > 40_000_000) throw new ValidationException("La imagen supera los 40 megapíxeles.");
        using var decoded = SKBitmap.Decode(codec) ?? throw new ValidationException("No pudimos leer esta imagen.");
        // Apply EXIF orientation before re-encoding; encoded output contains no GPS or camera metadata.
        var swap = (int)codec.EncodedOrigin >= 5;
        var w = decoded.Width; var h = decoded.Height;
        using var oriented = new SKBitmap(swap ? h : w, swap ? w : h);
        using (var canvas = new SKCanvas(oriented))
        {
            switch (codec.EncodedOrigin)
            {
                case SKEncodedOrigin.TopRight: canvas.Translate(w, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: canvas.Translate(w, h); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: canvas.Translate(0, h); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: canvas.Translate(h, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(h, w); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: canvas.Translate(0, w); canvas.RotateDegrees(-90); break;
            }
            canvas.DrawBitmap(decoded, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }
        var scale = Math.Min(1d, 2400d / Math.Max(oriented.Width, oriented.Height));
        using var resized = oriented.Resize(new SKImageInfo(Math.Max(1, (int)(oriented.Width * scale)), Math.Max(1, (int)(oriented.Height * scale))), new SKSamplingOptions(SKFilterMode.Linear));
        using var image = SKImage.FromBitmap(resized ?? oriented);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, 85);
        Directory.CreateDirectory(root);
        var name = Guid.NewGuid().ToString("N") + ".webp";
        await File.WriteAllBytesAsync(GetPath(name), encoded.ToArray());
        return name;
    }
}

public class GalleryService(IDbContextFactory<AppDbContext> factory, ICurrentUser currentUser, IImageStorage storage)
{
    private async Task<Celebration> EditableAsync(AppDbContext db, Guid eventId)
    {
        var owner = await currentUser.GetIdAsync();
        return await db.Events.Include(x => x.Gallery).ThenInclude(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == eventId && (x.OwnerId == owner || x.Collaborators.Any(c => c.UserId == owner)) && !x.IsDeleted) ?? throw new UnauthorizedAccessException();
    }

    public async Task AddAsync(Guid eventId, Stream stream, string caption)
    {
        await using var db = await factory.CreateDbContextAsync();
        var item = await EditableAsync(db, eventId);
        if (item.Gallery.Images.Count >= 100) throw new ValidationException("Cada galería admite hasta 100 imágenes.");
        var name = await storage.SaveAsync(stream);
        try
        {
            db.Images.Add(new GalleryImage { EventGalleryId = item.Gallery.Id, StorageName = name, Caption = caption[..Math.Min(caption.Length, 180)], Position = item.Gallery.Images.Count });
            await db.SaveChangesAsync();
        }
        catch { storage.Delete(name); throw; }
    }

    public async Task UpdateAsync(Guid eventId, Guid imageId, string caption, int direction = 0)
    {
        if (caption.Length > 180) throw new ValidationException("El texto puede tener hasta 180 caracteres.");
        await using var db = await factory.CreateDbContextAsync();
        var item = await EditableAsync(db, eventId);
        var images = item.Gallery.Images.OrderBy(x => x.Position).ThenBy(x => x.CreatedAtUtc).ToList();
        var photo = images.SingleOrDefault(x => x.Id == imageId) ?? throw new UnauthorizedAccessException();
        photo.Caption = caption;
        var index = images.IndexOf(photo); var target = Math.Clamp(index + Math.Sign(direction), 0, images.Count - 1);
        images.RemoveAt(index); images.Insert(target, photo);
        for (var i = 0; i < images.Count; i++) images[i].Position = i;
        await db.SaveChangesAsync();
    }

    public async Task SetCoverAsync(Guid eventId, Guid imageId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var item = await EditableAsync(db, eventId);
        if (!item.Gallery.Images.Any(x => x.Id == imageId)) throw new UnauthorizedAccessException();
        item.CoverImageId = imageId;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid eventId, Guid imageId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var item = await EditableAsync(db, eventId);
        var photo = item.Gallery.Images.SingleOrDefault(x => x.Id == imageId) ?? throw new UnauthorizedAccessException();
        if (item.CoverImageId == imageId) item.CoverImageId = null;
        db.Images.Remove(photo);
        var position = 0;
        foreach (var remaining in item.Gallery.Images.Where(x => x.Id != imageId).OrderBy(x => x.Position)) remaining.Position = position++;
        await db.SaveChangesAsync();
        storage.Delete(photo.StorageName);
    }
}
