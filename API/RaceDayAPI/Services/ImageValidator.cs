namespace RaceDayAPI.Services
{
    // Checks that an uploaded file is a small JPG, PNG or WEBP image.
    public static class ImageValidator
    {
        public const long MaxBytes = 2 * 1024 * 1024;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        // Returns an error message, or null when the file is acceptable
        public static async Task<string?> ValidateAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return "Please choose an image file.";
            }

            if (file.Length > MaxBytes)
            {
                return "The image must be 2 MB or smaller.";
            }

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return "Only JPG, PNG or WEBP images are allowed.";
            }

            // Check the first bytes of the file, because the name and content type can be faked
            var header = new byte[12];
            using var stream = file.OpenReadStream();
            int read = await stream.ReadAsync(header, 0, header.Length);

            bool isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            bool isPng = read >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
            bool isWebp = read >= 12
                && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

            if (!isJpeg && !isPng && !isWebp)
            {
                return "That file is not a valid JPG, PNG or WEBP image.";
            }

            return null;
        }
    }
}