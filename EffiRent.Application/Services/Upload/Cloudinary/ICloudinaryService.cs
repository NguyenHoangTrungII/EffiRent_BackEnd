using EffiAP.Domain.SeedWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Services.Upload.Cloudinary
{
    public interface ICloudinaryService : IScopedService
    {
        /// <summary>
        /// Uploads a photo to Cloudinary and returns the image URL.
        /// </summary>
        /// <param name="file">The IFormFile to upload.</param>
        /// <returns>The URL of the uploaded image.</returns>
        string UploadPhoto(IFormFile file);

        Task<string> UploadPhotoAsync(IFormFile file);

        Task DeletePhotoAsync(string publicId);

        string ExtractPublicId(string imageUrl);


    }

}
