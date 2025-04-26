using EffiAP.Application.Services.Upload.Cloudinary;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.FileProcessing
{
    public class FileProcessingService : IFileProcessingService
    {
        private readonly ICloudinaryService _cloudinaryService;
        private readonly ILogger<FileProcessingService> _logger;

        public FileProcessingService(ICloudinaryService cloudinaryService, ILogger<FileProcessingService> logger)
        {
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

        public async Task<List<string>> UploadImagesAsync(List<IFormFile> files)
        {
            var imageUrls = new List<string>();

            if (files == null || !files.Any())
            {
                _logger.LogInformation("No images provided for upload.");
                return imageUrls;
            }

            foreach (var file in files)
            {
                try
                {
                    var imageUrl = await _cloudinaryService.UploadPhotoAsync(file);
                    imageUrls.Add(imageUrl);
                    _logger.LogInformation("Uploaded image {FileName} to Cloudinary: {ImageUrl}", file.FileName, imageUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading image {FileName}", file.FileName);
                    throw;
                }
            }

            return imageUrls;
        }

        public async Task DeleteImageAsync(string imageUrl)
        {
            try
            {
                var publicId = _cloudinaryService.ExtractPublicId(imageUrl);
                await _cloudinaryService.DeletePhotoAsync(publicId);
                _logger.LogInformation("Deleted image {ImageUrl} from Cloudinary", imageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image {ImageUrl} from Cloudinary", imageUrl);
            }
        }
    }
}
