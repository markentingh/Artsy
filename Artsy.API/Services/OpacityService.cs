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

            // Offset all chroma keys by the color drift of the actual background
            // versus the first configured chroma key. The background is sampled from
            // the four corners, and the corner color closest to the first key is used
            // so the drift is computed from the real background, not a subject pixel.
            var chromaKeys = new List<(byte R, byte G, byte B)>(settings.ChromaKeys);
            if (chromaKeys.Count > 0)
            {
                var first = chromaKeys[0];
                var corners = new (int X, int Y)[]
                {
                    (0, 0),
                    (image.Width - 1, 0),
                    (0, image.Height - 1),
                    (image.Width - 1, image.Height - 1),
                };
                (byte R, byte G, byte B)? background = null;
                float minDistance = float.MaxValue;
                foreach (var (cx, cy) in corners)
                {
                    var cornerPixel = image[cx, cy];
                    var dr = (int)cornerPixel.R - first.R;
                    var dg = (int)cornerPixel.G - first.G;
                    var db = (int)cornerPixel.B - first.B;
                    var distance = MathF.Sqrt(dr * dr + dg * dg + db * db);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        background = (cornerPixel.R, cornerPixel.G, cornerPixel.B);
                    }
                }

                if (background.HasValue)
                {
                    var (br, bg, bb) = background.Value;
                    var driftR = br - first.R;
                    var driftG = bg - first.G;
                    var driftB = bb - first.B;
                    for (int i = 0; i < chromaKeys.Count; i++)
                    {
                        var key = chromaKeys[i];
                        chromaKeys[i] = (ClampByte(key.R + driftR), ClampByte(key.G + driftG), ClampByte(key.B + driftB));
                    }
                }
            }

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image[x, y];
                    float bestMatch = 0;

                    foreach (var (cr, cg, cb) in chromaKeys)
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
                        // Opacity strength is 4x the color match — pixels in the inner quarter of the range go fully transparent
                        var opacityStrength = Math.Min(1f, bestMatch * 4f);
                        var newAlpha = (byte)Math.Round(pixel.A * (1f - opacityStrength));
                        image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, newAlpha);
                    }
                }
            }

            using var stream = new MemoryStream();
            await image.SaveAsync(stream, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
            return stream.ToArray();
        }

        public async Task<byte[]> CompositeOverBackgroundAsync(byte[] pngBytes, byte[]? backgroundBytes, string? backgroundColor = null)
        {
            using var foreground = Image.Load<Rgba32>(pngBytes);
            using var canvas = new Image<Rgba32>(foreground.Width, foreground.Height);

            if (backgroundBytes != null && backgroundBytes.Length > 0)
            {
                using var bgImage = Image.Load(backgroundBytes);

                // Resize background to the foreground width if it's larger, maintaining aspect ratio.
                // Then center it on the canvas.
                if (bgImage.Width > foreground.Width)
                {
                    var scale = (double)foreground.Width / bgImage.Width;
                    var newH = (int)Math.Round(bgImage.Height * scale);
                    bgImage.Mutate(ctx => ctx.Resize(foreground.Width, newH));
                }

                // Center the background image on the canvas
                var bgX = (foreground.Width - bgImage.Width) / 2;
                var bgY = (foreground.Height - bgImage.Height) / 2;
                if (bgX < 0) bgX = 0;
                if (bgY < 0) bgY = 0;

                canvas.Mutate(ctx => ctx.DrawImage(bgImage, new Point(bgX, bgY), 1f));
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
            await image.SaveAsync(stream, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
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

        private static byte ClampByte(int value) => (byte)Math.Max(0, Math.Min(255, value));
    }
}
