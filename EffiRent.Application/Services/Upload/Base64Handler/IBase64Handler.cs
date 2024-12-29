using EffiAP.Domain.SeedWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Services.Upload.Base64Handler
{
    public interface IBase64Handler : ISingletonService
    {
        Task<List<string>> ConvertFilesToBase64Async(List<IFormFile> file);
        Task<IFormFile> ConvertBase64ToFileAsync(string fileBase64, string fileName = "file");
        Task<IFormFile> ConvertBytesToFileAsync(byte[] fileBytes, string fileName = "tempFile", string contentType = "application/octet-stream");

        Task<List<string>> CompressAndConvertFilesToBase64Async(IEnumerable<IFormFile> files);


    }
}
