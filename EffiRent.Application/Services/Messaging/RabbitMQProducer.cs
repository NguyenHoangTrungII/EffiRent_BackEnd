using EffiAP.Application.Services.Upload.Base64Handler;
using EffiAP.Domain.ViewModels.MaintainRequest;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EffiAP.Application.Services.Messaging
{
    public class RabbitMQProducer : IRabbitMQProducerService, IDisposable
    {
        private readonly IConnection _connection;
        //private readonly IBase64Handler _base64handler;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IModel _channel;

        public RabbitMQProducer( IServiceScopeFactory scopeFactory)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            var maintenanceQueueArgs = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", "retry_exchange" }, // Chỉ định DLX cho hàng đợi
                    { "x-dead-letter-routing-key", "maintenance_request" } // Routing key for DLX

                };
            _channel.QueueDeclare(queue: "maintenance_queue", durable: true, exclusive: false, autoDelete: false, arguments: maintenanceQueueArgs);
            _scopeFactory = scopeFactory;
        }

        public async Task QueueMaintenanceRequestAsync(MaintenanceRequestCommandDTO requestDto)
        {
            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestDto));

            // Thực hiện publish với async/await để xử lý tốt hơn trong môi trường bất đồng bộ
            await Task.Run(() =>
            {
                _channel.BasicPublish(exchange: "",
                                      routingKey: "maintenance_queue",
                                      basicProperties: null,
                                      body: body);
            });
        }

        public async Task SendToQueueWithTTLAsync(string queueName, MaintenanceRequestCommandDTO message)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // Không khai báo lại queue ở đây, chỉ gửi message
            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            // Gửi thông điệp vào hàng đợi mà không cần khai báo queue
            channel.BasicPublish(exchange: "maintenance_exchange",
                                 routingKey: "maintenance_request",
                                 basicProperties: properties,
                                 body: body);

            Console.WriteLine($"Message sent to queue {queueName}.");
        }


        //public async Task SendToRetryQueueAsync(MaintenanceRequestCommandDTO message, List<IFormFile> files)
        //{
        //    var factory = new ConnectionFactory() { HostName = "localhost" };
        //    using var connection = factory.CreateConnection();
        //    using var channel = connection.CreateModel();

        //    // Chuyển đổi IFormFile thành Base64
        //    List<string> fileBase64List = await _base64handler.ConvertFilesToBase64Async(files);
        //    //fileBase64 = await _base64handler.ConvertFileToBase64Async(file);
        //    //message.FileBase64 = fileBase64; // Thêm thuộc tính fileBase64 vào DTO

        //    //var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
        //    foreach (var file in files)
        //    {
        //        using var memoryStream = new MemoryStream();

        //        // Nén file
        //        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
        //        {
        //            await file.CopyToAsync(gzipStream);
        //        }

        //        // Chuyển đổi sang Base64
        //        fileBase64List.Add(Convert.ToBase64String(memoryStream.ToArray()));
        //    }

        //    MaintenanceMessage mess = new MaintenanceMessage
        //    {
        //        request = message,  // Đảm bảo thuộc tính Request được viết hoa nếu là public
        //        FileBase64 = fileBase64List
        //    };

        //    //// Chỉ gửi message vào retry queue mà không khai báo lại queue
        //    var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(mess));

        //    // Gửi thông điệp vào hàng đợi retry
        //    channel.BasicPublish(exchange: "retry_exchange",
        //                         routingKey: "maintenance_request",
        //                         basicProperties: null,
        //                         body: body);

        //    Console.WriteLine($"Message sent to retry queue.");
        //}

        public async Task SendToMaintenanceQueueAsync(MaintenanceRequestCommandDTO message, List<IFormFile> files)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            List<string> fileBase64List = new List<string>();

            try
            {

                using (var scope = _scopeFactory.CreateScope())
                {
                    var base64handler = scope.ServiceProvider.GetRequiredService<IBase64Handler>();
                    fileBase64List = await base64handler.CompressAndConvertFilesToBase64Async(files);
                }

                //        foreach (var file in files)
                //{
                //    using var memoryStream = new MemoryStream();

                //    // Nén file
                //    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
                //    {
                //        await file.CopyToAsync(gzipStream);
                //    }

                //    // Đặt lại vị trí về đầu stream để đọc dữ liệu đã nén
                //    memoryStream.Position = 0;

                //    // Chuyển đổi sang Base64
                //    fileBase64List.Add(Convert.ToBase64String(memoryStream.ToArray()));

                //}

                MaintenanceMessage mess = new MaintenanceMessage
                {
                    request = message,
                    FileBase64 = fileBase64List
                };

                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(mess));

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                // Gửi thông điệp vào hàng đợi retry
                channel.BasicPublish(exchange: "maintenance_exchange",
                                     routingKey: "maintenance_request",
                                     basicProperties: properties,
                                     body: body);

                Console.WriteLine($"Message sent to maintenance queue.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }
        }


        public async Task SendToRetryQueueAsync(string message)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            //List<string> fileBase64List = new List<string>();

            //foreach (var file in files)
            //{
            //    using var memoryStream = new MemoryStream();

            //    // Nén file
            //    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            //    {
            //        await file.CopyToAsync(gzipStream);
            //    }

            //    // Đặt lại vị trí về đầu stream để đọc dữ liệu đã nén
            //    memoryStream.Position = 0;

            //    // Chuyển đổi sang Base64
            //    fileBase64List.Add(Convert.ToBase64String(memoryStream.ToArray()));
            //}

            //MaintenanceMessage mess = new MaintenanceMessage
            //{
            //    request = message,
            //    FileBase64 = fileBase64List
            //};

            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

            // Gửi thông điệp vào hàng đợi retry
            channel.BasicPublish(exchange: "retry_exchange",
                                 routingKey: "maintenance_request",
                                 basicProperties: null,
                                 body: body);

            Console.WriteLine($"Message sent to retry queue.");
        }


        public async Task SendToCompletionQueueAsync(string requestDto)
        {
            // Sử dụng một kết nối và kênh riêng để gửi thông điệp
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // Gửi thông điệp lên completion_queue
            var completionMessage = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestDto));

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true; // Nếu bạn muốn đảm bảo thông điệp không bị mất

            channel.BasicPublish(exchange: "",
                                 routingKey: "completion_queue",
                                 basicProperties: properties,
                                 body: completionMessage);

            Console.WriteLine("Completion message sent.");
        }

        // Implement IDisposable để giải phóng kết nối khi không dùng nữa
        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
