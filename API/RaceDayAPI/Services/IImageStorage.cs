//This small interface lets the tests run without Azure

namespace RaceDayAPI.Services
{
    public interface IImageStorage
    {
        // Saves an image and returns its public URL
        Task<string> SaveAsync(IFormFile file, string folder);
    }
}