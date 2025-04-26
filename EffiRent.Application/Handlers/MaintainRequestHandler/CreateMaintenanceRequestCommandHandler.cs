using EffiAP.Application.Wrappers;
using EffiAP.Domain.Entities;
using EffiAP.Domain.Models;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Infrastructure.IRepositories;
using EffiRent.Application.Commands.MaintainRequestCommand;
using EffiRent.Application.Services.FileProcessing;
using EffiRent.Application.Services.Rabbit;
using MediatR;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EffiRent.Application.Handlers.MaintainRequestHandler
{
    public class CreateMaintenanceRequestCommandHandler : IRequestHandler<CreateMaintenanceRequestCommand, ApiResponse<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileProcessingService _fileProcessingService;
        private readonly IRabbitMQProducerService _rabbitMQProducer;
        private readonly ILogger<CreateMaintenanceRequestCommandHandler> _logger;

        public CreateMaintenanceRequestCommandHandler(
            IFileProcessingService fileProcessingService,
            IRabbitMQProducerService rabbitMQProducer,
            ILogger<CreateMaintenanceRequestCommandHandler> logger,
            IUnitOfWork unitOfWork)
        {
            _fileProcessingService = fileProcessingService ?? throw new ArgumentNullException(nameof(fileProcessingService));
            _rabbitMQProducer = rabbitMQProducer ?? throw new ArgumentNullException(nameof(rabbitMQProducer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<ApiResponse<bool>> Handle(CreateMaintenanceRequestCommand request, CancellationToken cancellationToken)
        {
            if (request == null || request.RequestDto == null)
            {
                _logger.LogWarning("Request or RequestDto is null.");
                return new ApiResponse<bool>("Request data is null") { Succeeded = false };
            }

            try
            {
                // Tải hình ảnh nếu có
                var fileUrls = new List<string>();
                if (request.Images != null && request.Images.Any())
                {
                    fileUrls = await _fileProcessingService.UploadImagesAsync(request.Images);
                    _logger.LogInformation("Uploaded {FileCount} images for request", fileUrls.Count);
                }

                // Tạo MaintenanceRequest
                var maintenanceRequest = new MaintenanceRequest
                {
                    Id = Guid.NewGuid(),
                    CustomerId = request.RequestDto.CustomerId,
                    Status = "Queue", // Trạng thái ban đầu
                    PriorityLevel = request.RequestDto.PriorityLevel, // Giả sử RequestDto có PriorityLevel
                    CategoryId = request.RequestDto.CategoryId, // Giả sử RequestDto có CategoryId
                    RoomId = request.RequestDto.RoomId, // Giả sử RequestDto có RoomId
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsCustomerConfirmed = false,
                    TechnicianConfirmed = false
                };

                // Bắt đầu transaction
                var transaction = await _unitOfWork.BeginTransactionAsync();

                try
                {
                    // Lưu MaintenanceRequest
                    await _unitOfWork.Repository.AddAsync(maintenanceRequest);

                    // Lưu MaintenanceRequestImage nếu có
                    if (fileUrls.Any())
                    {
                        var images = fileUrls.Select(url => new MaintenanceRequestImage
                        {
                            Id = Guid.NewGuid(),
                            MaintenanceRequestId = maintenanceRequest.Id,
                            ImageUrl = url,
                            CreatedAt = DateTime.UtcNow
                            //UploadedAt = DateTime.UtcNow
                        }).ToList();

                        await _unitOfWork.Repository.AddRangeAsync(images);
                    }

                    // Lưu thay đổi vào cơ sở dữ liệu
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    request.RequestDto.requestId = maintenanceRequest.Id;

                    // Tạo message cho RabbitMQ
                    var message = new RabbitMaintenanceMessage
                    {
                        request = request.RequestDto,
                        FileBase64 = fileUrls,
                        RequestId = maintenanceRequest.Id
                    };

                    // Gửi vào RabbitMQ
                    await _rabbitMQProducer.PublishAsync(message, "maintenance_exchange", "maintenance_request");
                    _logger.LogInformation("Sent maintenance request to maintenance_exchange for CustomerId {CustomerId}, RequestId {RequestId}",
                        request.RequestDto.CustomerId, maintenanceRequest.Id);

                    // Commit transaction
                    await _unitOfWork.CommitTransactionAsync(transaction);

                    return new ApiResponse<bool>("Maintenance request created and sent successfully")
                    {
                        Succeeded = true,
                        Data = true
                    };
                }
                catch (Exception ex)
                {
                    // Rollback transaction nếu có lỗi
                    await _unitOfWork.RollbackTransactionAsync(transaction);
                    _logger.LogError(ex, "Error during transaction for CustomerId {CustomerId}, RequestId {RequestId}",
                        request.RequestDto.CustomerId, maintenanceRequest.Id);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating maintenance request for CustomerId {CustomerId}", request.RequestDto.CustomerId);
                return new ApiResponse<bool>($"Error: {ex.Message}")
                {
                    Succeeded = false,
                    Errors = new List<string> { ex.ToString() }
                };
            }
        }
    }
}


//using EffiAP.Application.Wrappers;
//using EffiAP.Domain.Entities;
//using EffiAP.Domain.Models;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiAP.Infrastructure.IRepositories;
//using EffiRent.Application.Commands.MaintainRequestCommand;
//using EffiRent.Application.Services.FileProcessing;
//using EffiRent.Application.Services.Rabbit;
//using MediatR;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using System.Transactions;

//namespace EffiRent.Application.Handlers.MaintainRequestHandler
//{
//    public class CreateMaintenanceRequestCommandHandler : IRequestHandler<CreateMaintenanceRequestCommand, ApiResponse<bool>>
//    {
//        private readonly IUnitOfWork _unitOfWork;
//        private readonly IFileProcessingService _fileProcessingService;
//        private readonly IRabbitMQProducerService _rabbitMQProducer;
//        private readonly ILogger<CreateMaintenanceRequestCommandHandler> _logger;

//        public CreateMaintenanceRequestCommandHandler(
//            IFileProcessingService fileProcessingService,
//            IRabbitMQProducerService rabbitMQProducer,
//            ILogger<CreateMaintenanceRequestCommandHandler> logger,
//            IUnitOfWork unitOfWork)
//        {
//            _fileProcessingService = fileProcessingService ?? throw new ArgumentNullException(nameof(fileProcessingService));
//            _rabbitMQProducer = rabbitMQProducer ?? throw new ArgumentNullException(nameof(rabbitMQProducer));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//            _unitOfWork = unitOfWork;
//        }

//        public async Task<ApiResponse<bool>> Handle(CreateMaintenanceRequestCommand request, CancellationToken cancellationToken)
//        {
//            if (request == null || request.RequestDto == null)
//            {
//                _logger.LogWarning("Request or RequestDto is null.");
//                return new ApiResponse<bool>("Request data is null") { Succeeded = false };
//            }

//            try
//            {
//                var fileUrls = new List<string>();
//                if (request.Images != null && request.Images.Any())
//                {
//                    fileUrls = await _fileProcessingService.UploadImagesAsync(request.Images);
//                    _logger.LogInformation("Uploaded {FileCount} images for request", fileUrls.Count);
//                }
//                //var maintenanceRequest = new MaintenanceRequest
//                //{
//                //    Id = Guid.NewGuid(), // Tạo ID duy nhất cho yêu cầu
//                //    CustomerId = request.RequestDto.CustomerId, // ID của khách hàng
//                //    Status = "Queue", // Trạng thái ban đầu
//                //    PriorityLevel = request.RequestDto.PriorityLevel, // Giả sử RequestDto có PriorityLevel, nếu không thì gán mặc định (ví dụ: 3)
//                //    CreatedAt = DateTime.UtcNow, // Thời gian tạo
//                //    UpdatedAt = DateTime.UtcNow, // Thời gian cập nhật ban đầu
//                //    CategoryId = request.RequestDto.CategoryId, // Giả sử RequestDto có CategoryId
//                //    RoomId = request.RequestDto.RoomId, // Giả sử RequestDto có RoomId
//                //    IsCustomerConfirmed = false, // Mặc định chưa được khách hàng xác nhận
//                //    TechnicianConfirmed = false, // Mặc định chưa được kỹ thuật viên xác nhận
//                //    //Images = fileUrls.Select(url => new MaintenanceRequestImage
//                //    //{
//                //    //    ImageUrl = url,
//                //    //    UploadedAt = DateTime.UtcNow
//                //    //}).ToList() // Chuyển danh sách URL thành danh sách MaintenanceRequestImage
//                //};

//                var maintenanceRequest = new MaintenanceRequest
//                {
//                    CustomerId = request.RequestDto.CustomerId,
//                    Status = "Pending",
//                    PriorityLevel = request.RequestDto.PriorityLevel,
//                    CategoryId = request.RequestDto.CategoryId,
//                    RoomId = request.RequestDto.RoomId,
//                    CreatedAt = DateTime.UtcNow
//                };

//                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
//                {
//                    // Lưu yêu cầu vào cơ sở dữ liệu
//                    await _unitOfWork.Repository.AddAsync(maintenanceRequest);
//                    await _unitOfWork.SaveChangesAsync();

//                    var message = new RabbitMaintenanceMessage
//                    {
//                        request = request.RequestDto,
//                        FileBase64 = fileUrls,
//                        RequestId = maintenanceRequest.Id // Thêm ID của yêu cầu để sử dụng ở bước assign
//                    };

//                    // Gửi vào RabbitMQ
//                    await _rabbitMQProducer.PublishAsync(message, "maintenance_exchange", "maintenance_request");
//                    _logger.LogInformation("Sent maintenance request to maintenance_exchange for CustomerId {CustomerId}, RequestId {RequestId}",
//                        request.RequestDto.CustomerId, maintenanceRequest.Id);

//                    scope.Complete(); // Hoàn thành transaction
//                }

//                return new ApiResponse<bool>("Maintenance request created and sent successfully")
//                {
//                    Succeeded = true,
//                    Data = true
//                };
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error creating maintenance request for CustomerId {CustomerId}", request.RequestDto.CustomerId);
//                return new ApiResponse<bool>($"Error: {ex.Message}")
//                {
//                    Succeeded = false,
//                    Errors = new List<string> { ex.ToString() }
//                };
//            }
//        }
//    }
//}

//using EffiAP.Application.Wrappers;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiRent.Application.Commands.MaintainRequestCommand;
//using EffiRent.Application.Services.FileProcessing;
//using EffiRent.Application.Services.Rabbit;
//using MediatR;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace EffiRent.Application.Handlers.MaintainRequestHandler
//{
//    public class CreateMaintenanceRequestCommandHandler : IRequestHandler<CreateMaintenanceRequestCommand, ApiResponse<bool>>
//    {
//        private readonly IFileProcessingService _fileProcessingService;
//        private readonly IRabbitMQProducerService _rabbitMQProducer;
//        private readonly ILogger<CreateMaintenanceRequestCommandHandler> _logger;

//        public CreateMaintenanceRequestCommandHandler(
//            IFileProcessingService fileProcessingService,
//            IRabbitMQProducerService rabbitMQProducer,
//            ILogger<CreateMaintenanceRequestCommandHandler> logger)
//        {
//            _fileProcessingService = fileProcessingService ?? throw new ArgumentNullException(nameof(fileProcessingService));
//            _rabbitMQProducer = rabbitMQProducer ?? throw new ArgumentNullException(nameof(rabbitMQProducer));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public async Task<ApiResponse<bool>> Handle(CreateMaintenanceRequestCommand request, CancellationToken cancellationToken)
//        {
//            if (request == null || request.RequestDto == null)
//            {
//                _logger.LogWarning("Request or RequestDto is null.");
//                return new ApiResponse<bool>("Request data is null") { Succeeded = false };
//            }

//            try
//            {
//                var fileUrls = new List<string>();
//                if (request.Images != null && request.Images.Any())
//                {
//                    fileUrls = await _fileProcessingService.UploadImagesAsync(request.Images);
//                    _logger.LogInformation("Uploaded {FileCount} images for request", fileUrls.Count);
//                }

//                var message = new MaintenanceMessage
//                {
//                    request = request.RequestDto,
//                    FileBase64 = fileUrls
//                };

//                await _rabbitMQProducer.PublishAsync(message, "maintenance_exchange", "maintenance_request");
//                _logger.LogInformation("Sent maintenance request to maintenance_exchange for CustomerId {CustomerId}", request.RequestDto.CustomerId);

//                return new ApiResponse<bool>("Maintenance request sent successfully")
//                {
//                    Succeeded = true,
//                    Data = true
//                };
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error sending maintenance request for CustomerId {CustomerId}", request.RequestDto.CustomerId);
//                return new ApiResponse<bool>($"Error: {ex.Message}")
//                {
//                    Succeeded = false,
//                    Errors = new List<string> { ex.ToString() }
//                };
//            }
//        }
//    }
//}
