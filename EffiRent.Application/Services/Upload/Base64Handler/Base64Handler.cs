using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Services.Upload.Base64Handler
{
    public  class Base64Handler : IBase64Handler
    {

        public async Task<List<string>> ConvertFilesToBase64Async(List<IFormFile> files)
        {
            var base64Strings = new List<string>();

            foreach (var file in files)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    byte[] fileBytes = memoryStream.ToArray();
                    base64Strings.Add(Convert.ToBase64String(fileBytes));
                }
            }

            return base64Strings;
        }


        public async Task<IFormFile> ConvertBase64ToFileAsync(string fileBase64, string fileName = "file")
        {
            // Giải mã chuỗi base64 thành byte[]
            byte[] fileBytes = Convert.FromBase64String(fileBase64);

            // Chuyển đổi byte[] thành MemoryStream
            using var memoryStream = new MemoryStream(fileBytes);

            // Tạo đối tượng FormFile từ MemoryStream
            var formFile = new FormFile(memoryStream, 0, memoryStream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream" // Thay đổi ContentType tùy theo loại file
            };

            return formFile;
        }

        public async Task<IFormFile> ConvertBytesToFileAsync(byte[] fileBytes, string fileName = "tempFile", string contentType = "application/octet-stream")
        {
            var stream = new MemoryStream(fileBytes);
            var file = new FormFile(stream, 0, stream.Length, fileName, fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            return await Task.FromResult(file);
        }

        public async Task<List<string>> CompressAndConvertFilesToBase64Async(IEnumerable<IFormFile> files)
        {
            var fileBase64List = new List<string>();

            foreach (var file in files)
            {
                using var memoryStream = new MemoryStream();

                // Nén file
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
                {
                    await file.CopyToAsync(gzipStream);
                }

                // Đặt lại vị trí về đầu stream để đọc dữ liệu đã nén
                memoryStream.Position = 0;

                // Chuyển đổi sang Base64
                fileBase64List.Add(Convert.ToBase64String(memoryStream.ToArray()));
            }

            return fileBase64List;
        }



    }


}
