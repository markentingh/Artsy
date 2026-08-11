using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Artsy.API.Services
{
    public interface IOpacityService
    {
        /// <summary>
        /// Parses OpacityJson into chroma key colors, fuzziness, and background info.
        /// </summary>
        OpacitySettings? ParseOpacityJson(string? opacityJson);

        /// <summary>
        /// Applies chroma key colors with fuzziness to make matching pixels transparent.
        /// Returns PNG bytes with transparency.
        /// </summary>
        Task<byte[]> ApplyChromaKeysAsync(byte[] imageData, OpacitySettings settings);

        /// <summary>
        /// Composites a transparent PNG over a background image (resized to fill), producing a JPG.
        /// If backgroundBytes is null, uses the backgroundColor if provided, otherwise solid black.
        /// </summary>
        Task<byte[]> CompositeOverBackgroundAsync(byte[] pngBytes, byte[]? backgroundBytes, string? backgroundColor = null);

        /// <summary>
        /// Applies an overlay color to all non-transparent pixels in a PNG, preserving alpha.
        /// </summary>
        Task<byte[]> ApplyOverlayAsync(byte[] pngBytes, string overlayColor);
    }

    public class OpacitySettings
    {
        public List<(byte R, byte G, byte B)> ChromaKeys { get; set; } = new();
        public float Fuzziness { get; set; } = 0.0f;
        public BackgroundSettings? Background { get; set; }
        public OverlaySettings? Overlay { get; set; }
    }

    public class BackgroundSettings
    {
        public string Type { get; set; } = ""; // "artwork", "custom", or "color"
        public string Id { get; set; } = "";
        public string Color { get; set; } = ""; // hex color when Type == "color"
    }

    public class OverlaySettings
    {
        public string Color { get; set; } = ""; // hex color
    }

    public class OpacityService : IOpacityService
    {
        public OpacitySettings? ParseOpacityJson(string? opacityJson)
        {
            if (string.IsNullOrWhiteSpace(opacityJson))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(opacityJson);
                var root = doc.RootElement;

                var settings = new OpacitySettings();

                if (root.TryGetProperty("chromakeys", out var ckEl) && ckEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in ckEl.EnumerateArray())
                    {
                        var hex = item.GetString();
                        if (!string.IsNullOrWhiteSpace(hex))
                        {
                            var (r, g, b) = ParseHexColor(hex);
                            settings.ChromaKeys.Add((r, g, b));
                        }
                    }
                }

                if (root.TryGetProperty("fuziness", out var fuzzEl))
                {
                    settings.Fuzziness = fuzzEl.ValueKind == JsonValueKind.Number ? fuzzEl.GetSingle() : 0.0f;
                }

                if (root.TryGetProperty("background", out var bgEl) && bgEl.ValueKind == JsonValueKind.Object)
                {
                    var bg = new BackgroundSettings();
                    if (bgEl.TryGetProperty("type", out var typeEl))
                        bg.Type = typeEl.GetString() ?? "";
                    if (bgEl.TryGetProperty("id", out var idEl))
                        bg.Id = idEl.GetString() ?? "";
                    if (bgEl.TryGetProperty("color", out var colorEl))
                        bg.Color = colorEl.GetString() ?? "";
                    settings.Background = bg;
                }

                if (root.TryGetProperty("overlay", out var overlayEl) && overlayEl.ValueKind == JsonValueKind.Object)
                {
                    var overlay = new OverlaySettings();
                    if (overlayEl.TryGetProperty("color", out var overlayColorEl))
                        overlay.Color = overlayColorEl.GetString() ?? "";
                    settings.Overlay = overlay;
                }

                return settings;
            }
            catch
            {
                return null;
            }
        }

        public async Task<byte[]> ApplyChromaKeysAsync(byte[] imageData, OpacitySettings settings)
        {
            using var image = Image.Load<Rgba32>(imageData);
            var fuzziness = settings.Fuzziness;
            // Fuzziness is a direct distance threshold (1-200), same as ReplaceColor.jsx
            var maxDistance = fuzziness;
            if (maxDistance <= 0) maxDistance = 1f;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image[x, y];
                    float bestMatch = 0;

                    foreach (var (cr, cg, cb) in settings.ChromaKeys)
                    {
                        var dr = pixel.R - cr;
                        var dg = pixel.G - cg;
                        var db = pixel.B - cb;
                        var distance = MathF.Sqrt(dr * dr + dg * dg + db * db);
                        if (distance <= maxDistance)
                        {
                            var match = 1f - (distance / maxDistance);
                            if (match > bestMatch) bestMatch = match;
                        }
                    }

                    if (bestMatch > 0)
                    {
                        // Opacity strength is 2x the color match — pixels in the inner half of the range go fully transparent
                        var opacityStrength = Math.Min(1f, bestMatch * 2f);
                        var newAlpha = (byte)Math.Round(pixel.A * (1f - opacityStrength));
                        image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, newAlpha);
                    }
                }
            }

            using var stream = new MemoryStream();
            await image.SaveAsync(stream, new PngEncoder());
            return stream.ToArray();
        }

        public async Task<byte[]> CompositeOverBackgroundAsync(byte[] pngBytes, byte[]? backgroundBytes, string? backgroundColor = null)
        {
            using var foreground = Image.Load<Rgba32>(pngBytes);
            using var canvas = new Image<Rgba32>(foreground.Width, foreground.Height);

            if (backgroundBytes != null && backgroundBytes.Length > 0)
            {
                using var bgImage = Image.Load(backgroundBytes);

                // Resize background to fill the foreground dimensions (cover mode)
                bgImage.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(foreground.Width, foreground.Height),
                    Mode = ResizeMode.Crop
                }));

                // Draw background onto canvas
                canvas.Mutate(ctx => ctx.DrawImage(bgImage, new Point(0, 0), 1f));
            }
            else
            {
                // Use background color if provided, otherwise solid black
                var bgColor = Color.Black;
                if (!string.IsNullOrWhiteSpace(backgroundColor))
                {
                    var (r, g, b) = ParseHexColor(backgroundColor);
                    bgColor = Color.FromRgb(r, g, b);
                }
                canvas.Mutate(ctx => ctx.BackgroundColor(bgColor));
            }

            // Draw the transparent PNG on top
            canvas.Mutate(ctx => ctx.DrawImage(foreground, new Point(0, 0), 1f));

            using var stream = new MemoryStream();
            await canvas.SaveAsync(stream, new JpegEncoder { Quality = 90 });
            return stream.ToArray();
        }

        public async Task<byte[]> ApplyOverlayAsync(byte[] pngBytes, string overlayColor)
        {
            using var image = Image.Load<Rgba32>(pngBytes);
            var (or, og, ob) = ParseHexColor(overlayColor);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image[x, y];
                    if (pixel.A > 0)
                    {
                        // Replace RGB with overlay color, preserve alpha
                        image[x, y] = new Rgba32(or, og, ob, pixel.A);
                    }
                }
            }

            using var stream = new MemoryStream();
            await image.SaveAsync(stream, new PngEncoder());
            return stream.ToArray();
        }

        private static (byte r, byte g, byte b) ParseHexColor(string hex)
        {
            var cleaned = hex.TrimStart('#');
            if (cleaned.Length >= 6)
            {
                var r = byte.Parse(cleaned.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                var g = byte.Parse(cleaned.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                var b = byte.Parse(cleaned.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return (r, g, b);
            }
            return (0, 0, 0);
        }
    }
}
