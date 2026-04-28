using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace CourseInventory.Web.Services;

public interface IImageService
{
    Task<string?> UploadAsync(IFormFile? file);
}

public class ImageService(IConfiguration config) : IImageService
{
    public async Task<string?> UploadAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;
        var cloudName = config["Cloudinary:CloudName"];
        var apiKey = config["Cloudinary:ApiKey"];
        var apiSecret = config["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            return null;

        var cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));
        await using var stream = file.OpenReadStream();
        var result = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "course-inventory"
        });
        return result.SecureUrl?.ToString();
    }
}
