using EffiAP.Application.Services.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text.Json;
using System.Threading.Tasks;


namespace EffiRent.Application.Services.Rabbit
{
    public class RabbitMQConsumerServices : IRabbitMQConsumerService
    {
        private readonly ILogger<RabbitMQConsumerServices> _logger;

        public RabbitMQConsumerServices(ILogger<RabbitMQConsumerServices> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessMessageAsync(IModel channel, BasicDeliverEventArgs eventArgs, string message)
        {
            try
            {
                _logger.LogInformation("Processing message: {Message}", message);

                // Deserialize message
                var data = JsonSerializer.Deserialize<TechnicianAssignedMessage>(message);
                if (data == null)
                {
                    _logger.LogWarning("Failed to deserialize message: {Message}", message);
                    channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                    return;
                }

                // TODO: Thêm logic xử lý message
                // Ví dụ: Gửi thông báo, cập nhật database, hoặc gọi API
                _logger.LogInformation(
                    "Processed technician assignment: MaintenanceRequestId={MaintenanceRequestId}, TechnicianId={TechnicianId}, Status={Status}",
                    data.MaintenanceRequestId, data.TechnicianId, data.Status
                );

                // Xác nhận message
                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message: {Message}", message);
                // Không ack, để message được retry hoặc chuyển vào retry_queue
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }

            await Task.CompletedTask;
        }

        public async Task MoveRequestsFromRetryToMaintenanceQueueAsync(IModel channel)
        {
            try
            {
                _logger.LogInformation("Checking retry_queue for pending requests...");

                // TODO: Thêm logic để di chuyển message từ retry_queue sang maintenance_queue
                // Ví dụ: Kiểm tra kỹ thuật viên khả dụng và publish lại message
                _logger.LogInformation("Moved requests from retry_queue to maintenance_queue (placeholder).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving requests from retry_queue to maintenance_queue");
            }

            await Task.CompletedTask;
        }

        private class TechnicianAssignedMessage
        {
            public Guid MaintenanceRequestId { get; set; }
            public string CustomerId { get; set; }
            public string TechnicianId { get; set; }
            public string Status { get; set; }
            public string PriorityLevel { get; set; }
            public string CategoryId { get; set; }
            public string RoomId { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
