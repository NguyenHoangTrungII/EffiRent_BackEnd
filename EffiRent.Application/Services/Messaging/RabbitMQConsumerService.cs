using EffiAP.Application.Commands.MaintainRequestCommand;
using EffiAP.Application.Services.Messaging;
using EffiAP.Application.Services.Upload.Base64Handler;
using EffiAP.Domain.ViewModels.MaintainRequest;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.IO.Compression;
using System.Text;

namespace EffiAP.Application.Services.Messaging
{
    public class RabbitMQConsumerService : IRabbitMQConsumerService
    {
        private readonly ILogger<RabbitMQConsumerService> _logger;
        //private readonly IBase64Handler _base64handler;
        private readonly IServiceScopeFactory _scopeFactory;
        //private readonly IMediator _mediator;

        public RabbitMQConsumerService(ILogger<RabbitMQConsumerService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            //_base64handler = base64handler;
            //_mediator = mediator;
        }

        public async Task MoveRequestsFromRetryToMaintenanceQueueAsync(IModel channel)
        {
            var result = channel.BasicGet(queue: "retry_queue", autoAck: false);
            if (result != null)
            {
                try
                {
                    channel.BasicPublish(
                        exchange: "maintenance_exchange",
                        routingKey: "maintenance_request",
                        basicProperties: result.BasicProperties,
                        body: result.Body.ToArray()
                    );

                    _logger.LogInformation("Request moved from retry_queue to maintenance_queue.");
                    channel.BasicAck(deliveryTag: result.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to move message from retry_queue to maintenance_queue: {ex.Message}");
                }
            }
            else
            {
                _logger.LogInformation("No message available in retry_queue.");
            }
        }

        public async Task ProcessMessageAsync(IModel channel, BasicDeliverEventArgs ea, string message)
        {
            try
            {
                List<IFormFile> imgs = new List<IFormFile>();
                var maintenanceMessage = JsonConvert.DeserializeObject<MaintenanceMessage>(message);
                // Lấy dữ liệu từ đối tượng MaintenanceMessage
                MaintenanceRequestCommandDTO mess = maintenanceMessage.request;
                List<string> fileBase64List = maintenanceMessage.FileBase64;

                // Sử dụng dữ liệu đã tách ra
                _logger.LogInformation("Received message with MaintenanceRequestCommandDTO and FileBase64.");

                using (var scope = _scopeFactory.CreateScope())
                {
                    var base64handler = scope.ServiceProvider.GetRequiredService<IBase64Handler>();
                    // Chuyển đổi fileBase64 trở lại thành byte[] nếu cần để xử lý thêm
                    if (fileBase64List != null && fileBase64List.Count > 0)
                    {
                        foreach (var fileBase64 in fileBase64List)
                        {
                            var decompressedBytes = DecompressBase64(fileBase64);
                            var file = await base64handler.ConvertBytesToFileAsync(decompressedBytes);
                            if (file != null)
                            {
                                imgs.Add(file);
                            }
                        }
                    }

                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var command = new AssignTechnicianCommand(mess, imgs);
                    var respone = await mediator.Send(command);

                    if (respone.Succeeded)
                    {
                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    else
                    {
                        channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    }
                }
            }
            catch
            {
                channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
        }
            

        public byte[] DecompressBase64(string base64String)
        {
            var compressedBytes = Convert.FromBase64String(base64String);
            using var compressedStream = new MemoryStream(compressedBytes);
            using var decompressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(decompressedStream);
            }
            return decompressedStream.ToArray();
        }

    }
}
