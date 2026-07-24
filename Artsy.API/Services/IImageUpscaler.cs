namespace Artsy.API.Services;

public interface IImageUpscaler
{
    Task<byte[]> UpscaleAsync(byte[] imageBytes);
}
