using EffiAP.Domain.SeedWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.FileProcessing
{
    public interface IFileProcessingService : IScopedService
    {
        Task<List<string>> UploadImagesAsync(List<IFormFile> files);
        Task DeleteImageAsync(string imageUrl);
    }
}
