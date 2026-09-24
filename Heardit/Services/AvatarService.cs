using System.Security.Cryptography;
using Heardit.Areas.Identity.Data;
using Heardit.Models;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Heardit.Services
{
    public enum AvatarUploadStatus
    {
        Saved,
        Empty,
        TooLarge,
        Unsupported
    }

    public record AvatarUploadResult(AvatarUploadStatus Status, string? Version = null);

    public record StoredAvatar(byte[] Data, string ContentType, string Version);

    public interface IAvatarService
    {
        Task<AvatarUploadResult> SetAsync(string userId, Stream upload, long length);

        Task RemoveAsync(string userId);

        /// <summary>The photo, or null when the user has none.</summary>
        Task<StoredAvatar?> GetAsync(string userId);
    }

    public class AvatarService : IAvatarService
    {
        private readonly HearditDbContext _context;

        public AvatarService(HearditDbContext context) => _context = context;

        public async Task<AvatarUploadResult> SetAsync(string userId, Stream upload, long length)
        {
            if (length <= 0)
            {
                return new AvatarUploadResult(AvatarUploadStatus.Empty);
            }

            if (length > AvatarImage.MaxUploadBytes)
            {
                return new AvatarUploadResult(AvatarUploadStatus.TooLarge);
            }

            using var buffer = new MemoryStream();
            await upload.CopyToAsync(buffer);
            var webp = AvatarImage.Process(buffer.ToArray());
            if (webp == null)
            {
                return new AvatarUploadResult(AvatarUploadStatus.Unsupported);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return new AvatarUploadResult(AvatarUploadStatus.Unsupported);
            }

            var avatar = await _context.UserAvatars.FirstOrDefaultAsync(a => a.UserId == userId);
            if (avatar == null)
            {
                avatar = new UserAvatar { UserId = userId };
                _context.UserAvatars.Add(avatar);
            }

            avatar.Data = webp;
            avatar.ContentType = "image/webp";
            avatar.UpdatedAt = DateTime.UtcNow;

            // The version is a content hash: a new photo gets a new URL, the same photo keeps its cache.
            user.AvatarVersion = Convert.ToHexString(SHA256.HashData(webp))[..16].ToLowerInvariant();
            await _context.SaveChangesAsync();

            return new AvatarUploadResult(AvatarUploadStatus.Saved, user.AvatarVersion);
        }

        public async Task RemoveAsync(string userId)
        {
            await _context.UserAvatars.Where(a => a.UserId == userId).ExecuteDeleteAsync();
            await _context.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.AvatarVersion, (string?)null));
        }

        public async Task<StoredAvatar?> GetAsync(string userId)
        {
            var row = await _context.UserAvatars
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => new { a.Data, a.ContentType, a.User.AvatarVersion })
                .FirstOrDefaultAsync();

            return row == null || row.AvatarVersion == null
                ? null
                : new StoredAvatar(row.Data, row.ContentType, row.AvatarVersion);
        }
    }

    /// <summary>
    /// Turns an uploaded photo into the stored avatar: centre-cropped square, 256px, WebP. Re-encoding
    /// also drops every bit of metadata, including the GPS position phones write into photos.
    /// </summary>
    public static class AvatarImage
    {
        public const long MaxUploadBytes = 5 * 1024 * 1024;
        public const int Size = 256;

        // Refuse to decode anything bigger than ~40 megapixels: a tiny file can claim a huge canvas.
        private const long MaxSourcePixels = 40_000_000;

        private static readonly HashSet<SKEncodedImageFormat> Accepted = new()
        {
            SKEncodedImageFormat.Jpeg,
            SKEncodedImageFormat.Png,
            SKEncodedImageFormat.Webp,
            SKEncodedImageFormat.Gif,
            SKEncodedImageFormat.Heif,
            SKEncodedImageFormat.Avif
        };

        /// <summary>The WebP bytes, or null when the upload isn't an image we can read.</summary>
        public static byte[]? Process(byte[] upload)
        {
            using var data = SKData.CreateCopy(upload);
            using var codec = SKCodec.Create(data);
            if (codec == null || !Accepted.Contains(codec.EncodedFormat))
            {
                return null;
            }

            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxSourcePixels)
            {
                return null;
            }

            using var decoded = SKBitmap.Decode(codec);
            if (decoded == null)
            {
                return null;
            }

            using var upright = ApplyOrientation(decoded, codec.EncodedOrigin);

            // Centre crop to a square, then scale down with a good filter.
            var side = Math.Min(upright.Width, upright.Height);
            var crop = new SKRectI((upright.Width - side) / 2, (upright.Height - side) / 2, 0, 0);
            crop.Right = crop.Left + side;
            crop.Bottom = crop.Top + side;

            using var square = new SKBitmap(side, side);
            if (!upright.ExtractSubset(square, crop))
            {
                return null;
            }

            var target = Math.Min(Size, side);
            using var resized = square.Resize(
                new SKImageInfo(target, target, SKColorType.Rgba8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (resized == null)
            {
                return null;
            }

            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Webp, 82);
            return encoded?.ToArray();
        }

        /// <summary>Phones store photos sideways and record the rotation in EXIF; bake it in before cropping.</summary>
        private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
        {
            if (origin == SKEncodedOrigin.TopLeft || origin == SKEncodedOrigin.Default)
            {
                return source.Copy();
            }

            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var width = swap ? source.Height : source.Width;
            var height = swap ? source.Width : source.Height;

            var result = new SKBitmap(width, height, source.ColorType, source.AlphaType);
            using var canvas = new SKCanvas(result);
            switch (origin)
            {
                case SKEncodedOrigin.TopRight: canvas.Scale(-1, 1, width / 2f, 0); break;
                case SKEncodedOrigin.BottomRight: canvas.RotateDegrees(180, width / 2f, height / 2f); break;
                case SKEncodedOrigin.BottomLeft: canvas.Scale(1, -1, 0, height / 2f); break;
                case SKEncodedOrigin.LeftTop: canvas.Translate(width, 0); canvas.RotateDegrees(90); canvas.Scale(1, -1, 0, source.Height / 2f); break;
                case SKEncodedOrigin.RightTop: canvas.Translate(width, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(0, height); canvas.RotateDegrees(-90); canvas.Scale(1, -1, 0, source.Height / 2f); break;
                case SKEncodedOrigin.LeftBottom: canvas.Translate(0, height); canvas.RotateDegrees(-90); break;
            }

            using var sourceImage = SKImage.FromBitmap(source);
            canvas.DrawImage(sourceImage, 0, 0, SKSamplingOptions.Default);
            return result;
        }
    }
}
