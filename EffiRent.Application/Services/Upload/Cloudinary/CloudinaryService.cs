﻿using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql.BackendMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Services.Upload.Cloudinary
{
    public class CloudinaryService : ICloudinaryService
    {
        //private readonly CloudinaryDotNet.Cloudinary _cloudinary;
        //public string  CLOUD_NAME = "dtdpz7tk5";
        //public string API_KEY = "634851463263782";
        //public string API_SECRET="16v14uvQ1D4eMfJnQRYK_z2YYRg";

        //public CloudinaryService()
        //{
        //    var account = new CloudinaryDotNet.Account(CLOUD_NAME, API_KEY, API_SECRET);
        //    _cloudinary = new CloudinaryDotNet.Cloudinary(account);
        //}

        private readonly CloudinaryDotNet.Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryService> _logger; // Thêm logger
        private const string CLOUD_NAME = "dtdpz7tk5";
        private const string API_KEY = "634851463263782";
        private const string API_SECRET = "16v14uvQ1D4eMfJnQRYK_z2YYRg";

        public CloudinaryService(ILogger<CloudinaryService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var account = new Account(CLOUD_NAME, API_KEY, API_SECRET);
            _cloudinary = new CloudinaryDotNet.Cloudinary(account);
            _cloudinary.Api.Secure = true; // Đảm bảo sử dụng HTTPS
        }

        public async Task<string> UploadPhotoAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("UploadPhotoAsync: File is null or empty.");
                throw new ArgumentException("File is null or empty.");
            }

            try
            {
                using var stream = file.OpenReadStream();
                var uploadResult = await UploadToCloudinaryAsync(stream, file.FileName);
                ValidateUploadResult(uploadResult);
                var imageUrl = GenerateImageUrl(uploadResult);
                _logger.LogInformation("Successfully uploaded image: {ImageUrl}", imageUrl);
                return imageUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image: {FileName}", file.FileName);
                throw;
            }
        }

        public string UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File is null or empty.");
            }

            using (var stream = file.OpenReadStream())
            {
                var uploadResult = UploadToCloudinary(stream, file.FileName);
                ValidateUploadResult(uploadResult);
                return GenerateImageUrl(uploadResult);
            }
        }

        public async Task DeletePhotoAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId);
            await _cloudinary.DestroyAsync(deleteParams);
        }

        public string ExtractPublicId(string imageUrl)
        {
            // Assuming the imageUrl follows the format: https://res.cloudinary.com/{cloud_name}/image/upload/v{version}/{public_id}.{format}
            var uri = new Uri(imageUrl);
            var segments = uri.Segments;

            // The public ID is usually the second-to-last segment before the file extension
            if (segments.Length >= 2)
            {
                var publicIdWithExtension = segments[^1]; // Get the last segment
                var publicId = publicIdWithExtension.Split('.')[0]; // Remove the extension
                return publicId;
            }

            throw new Exception("Invalid image URL format.");
        }


        private ImageUploadResult UploadToCloudinary(Stream stream, string fileName)
        {
            var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams()
            {
                File = new FileDescription(fileName, stream)
            };

            return _cloudinary.Upload(uploadParams);
        }

        private async Task<ImageUploadResult> UploadToCloudinaryAsync(Stream stream, string fileName)
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream)
            };

            return await _cloudinary.UploadAsync(uploadParams);
        }

        private void ValidateUploadResult(ImageUploadResult uploadResult)
        {
            if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Error uploading image: {uploadResult.Error.Message}");
            }
        }

        private string GenerateImageUrl(ImageUploadResult uploadResult)
        {
            return _cloudinary.Api.UrlImgUp.BuildUrl($"{uploadResult.PublicId}.{uploadResult.Format}");
        }
    }

}
