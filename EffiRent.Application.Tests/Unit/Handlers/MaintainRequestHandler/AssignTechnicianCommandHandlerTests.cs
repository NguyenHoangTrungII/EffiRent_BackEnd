//using EffiAP.Application.Commands.MaintainRequestCommand;
//using EffiAP.Application.Handlers.MaintainRequestHandler;
//using EffiRent.Application.Services.Rabbit;
//using EffiAP.Application.Services.Upload.Cloudinary;
//using EffiAP.Application.Wrappers;
//using EffiAP.Domain.Entities;
//using EffiAP.Domain.Models;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiAP.Infrastructure.IRepositories;
//using MediatR;
//using Microsoft.AspNetCore.Http;
//using Microsoft.EntityFrameworkCore.Infrastructure;
//using Microsoft.EntityFrameworkCore.Storage;
//using Microsoft.Extensions.Logging;
//using Moq;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Threading;
//using System.Threading.Tasks;
//using Xunit;
//using Microsoft.EntityFrameworkCore;

//namespace EffiAP.Application.Tests.Handlers
//{
//    public class AssignTechnicianCommandHandlerTests
//    {
//        private readonly Mock<IApplicationRoleRepository> _roleRepoMock;
//        private readonly Mock<IApplicationUserRoleRepository> _userRoleRepoMock;
//        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
//        private readonly Mock<IGenericRepository> _genericRepoMock;
//        private readonly Mock<IRabbitMQProducerService> _rabbitMQProducerMock;
//        private readonly Mock<ICloudinaryService> _cloudinaryServiceMock;
//        private readonly Mock<ILogger<AssignTechnicianCommandHandler>> _loggerMock;
//        private readonly AssignTechnicianCommandHandler _handler;

//        public AssignTechnicianCommandHandlerTests()
//        {
//            _roleRepoMock = new Mock<IApplicationRoleRepository>();
//            _userRoleRepoMock = new Mock<IApplicationUserRoleRepository>();
//            _unitOfWorkMock = new Mock<IUnitOfWork>();
//            _genericRepoMock = new Mock<IGenericRepository>();
//            _rabbitMQProducerMock = new Mock<IRabbitMQProducerService>();
//            _cloudinaryServiceMock = new Mock<ICloudinaryService>();
//            _loggerMock = new Mock<ILogger<AssignTechnicianCommandHandler>>();

//            // Setup IUnitOfWork to return IGenericRepository
//            _unitOfWorkMock.Setup(u => u.Repository).Returns(_genericRepoMock.Object);
//            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(new Mock<IDbContextTransaction>().Object);
//            _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>())).Returns(Task.CompletedTask);
//            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<IDbContextTransaction>())).Returns(Task.CompletedTask);
//            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

//            _handler = new AssignTechnicianCommandHandler(
//                _roleRepoMock.Object,
//                _userRoleRepoMock.Object,
//                _unitOfWorkMock.Object,
//                _rabbitMQProducerMock.Object,
//                _cloudinaryServiceMock.Object,
//                _loggerMock.Object);
//        }

//        [Fact]
//        public async Task Handle_ValidRequestWithAvailableTechnician_ReturnsSuccess()
//        {
//            // Arrange
//            var requestDto = new MaintenanceRequestCommandDTO
//            {
//                CustomerId = "customer1",
//                PriorityLevel = 1,
//                CategoryId = Guid.NewGuid(),
//                RoomId = Guid.NewGuid(),
//                //Description = "Test request"
//            };
//            var command = new AssignTechnicianCommand(requestDto);

//            // Mock role repository
//            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");

//            // Mock user role repository
//            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id"))
//                .ReturnsAsync(new List<string> { "tech1", "tech2" });

//            // Mock Get<MaintenanceRequest> with explicit parameters
//            var maintenanceRequests = new List<MaintenanceRequest>();
//            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>(), It.IsAny<Expression<Func<MaintenanceRequest, object>>[]>()))
//                .Returns(maintenanceRequests.AsQueryable());

//            // Mock Get<MaintenanceRequest> with null predicate and no includes
//            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(null, It.IsAny<Expression<Func<MaintenanceRequest, object>>[]>()))
//                .Returns(maintenanceRequests.AsQueryable());

//            // Mock AddAsync for MaintenanceRequest
//            Guid generatedId = Guid.NewGuid();
//            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
//                .Callback<MaintenanceRequest>(mr => mr.Id = generatedId);

//            // Mock SaveChangesAsync and transaction
//            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
//            _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>())).Returns(Task.CompletedTask);

//            // Mock RabbitMQ publish
//            _rabbitMQProducerMock.Setup(r => r.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
//                .Returns(Task.CompletedTask);

//            // Act
//            var result = await _handler.Handle(command, CancellationToken.None);

//            // Assert
//            Assert.True(result.Succeeded, $"Expected Succeeded to be true, but got false. Message: {result.Message}");
//            Assert.NotNull(result.Data);
//            Assert.Equal(generatedId, result.Data.requestId);
//            Assert.Equal("tech1", result.Data.TechnicianId);
//            Assert.Equal("Pending", result.Data.Status);
//            Assert.Equal(requestDto.Description, result.Data.Description);
//            Assert.Equal(requestDto.CustomerId, result.Data.CustomerId);
//            Assert.Equal(requestDto.PriorityLevel, result.Data.PriorityLevel);
//            Assert.Equal(requestDto.CategoryId, result.Data.CategoryId);
//            Assert.Equal(requestDto.RoomId, result.Data.RoomId);
//            _rabbitMQProducerMock.Verify(r => r.PublishAsync(It.IsAny<object>(), "maintenance_exchange", "maintenance_request"), Times.Once());
//            _unitOfWorkMock.Verify(r => r.CommitTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Once());
//            _genericRepoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceRequest>()), Times.Once());
//        }

//        [Fact]
//        public async Task Handle_NoAvailableTechnicians_ReturnsQueuedResponse()
//        {
//            // Arrange
//            var requestDto = new MaintenanceRequestCommandDTO
//            {
//                CustomerId = "customer1",
//                PriorityLevel = 1,
//                CategoryId = Guid.NewGuid(),
//                RoomId = Guid.NewGuid(),
//                Description = "Test request"
//            };
//            var command = new AssignTechnicianCommand(requestDto);

//            // Mock role repository
//            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");

//            // Mock user role repository
//            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id"))
//                .ReturnsAsync(new List<string> { "tech1" });

//            // Mock Get<MaintenanceRequest> to return pending requests for all technicians
//            var maintenanceRequests = new List<MaintenanceRequest>
//            {
//                new MaintenanceRequest { Id = Guid.NewGuid(), Status = "Pending", TechnicianId = "tech1" }
//            };
//            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>(), It.IsAny<Expression<Func<MaintenanceRequest, object>>[]>()))
//                .Returns(maintenanceRequests.AsQueryable());
//            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(null, It.IsAny<Expression<Func<MaintenanceRequest, object>>[]>()))
//                .Returns(maintenanceRequests.AsQueryable());

//            // Mock RabbitMQ publish
//            _rabbitMQProducerMock.Setup(r => r.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
//                .Returns(Task.CompletedTask);

//            // Act
//            var result = await _handler.Handle(command, CancellationToken.None);

//            // Assert
//            Assert.False(result.Succeeded);
//            Assert.Equal("No available technicians; request has been queued.", result.Message);
//            Assert.NotNull(result.Data);
//            Assert.Equal(requestDto.CustomerId, result.Data.CustomerId);
//            Assert.Equal(requestDto.PriorityLevel, result.Data.PriorityLevel);
//            Assert.Equal(requestDto.CategoryId, result.Data.CategoryId);
//            Assert.Equal(requestDto.RoomId, result.Data.RoomId);
//            Assert.Equal(requestDto.Description, result.Data.Description);
//            _rabbitMQProducerMock.Verify(r => r.PublishAsync(It.IsAny<object>(), "maintenance_exchange", "maintenance_request"), Times.Once());
//            _genericRepoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceRequest>()), Times.Never());
//            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Never());
//        }

//        [Fact]
//        public async Task Handle_TechnicianRoleNotFound_ReturnsError()
//        {
//            // Arrange
//            var requestDto = new MaintenanceRequestCommandDTO
//            {
//                CustomerId = "customer1",
//                PriorityLevel = 1,
//                CategoryId = Guid.NewGuid(),
//                RoomId = Guid.NewGuid(),
//                Description = "Test request"
//            };
//            var command = new AssignTechnicianCommand(requestDto);

//            // Mock role repository to return null
//            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync((string)null);

//            // Act
//            var result = await _handler.Handle(command, CancellationToken.None);

//            // Assert
//            Assert.False(result.Succeeded);
//            Assert.Equal("Technician role does not exist.", result.Message);
//            Assert.Null(result.Data);
//            _userRoleRepoMock.Verify(r => r.GetTechniciansAsync(It.IsAny<string>()), Times.Never());
//            _genericRepoMock.Verify(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>(), It.IsAny<Expression<Func<MaintenanceRequest, object>>[]>()), Times.Never());
//            _genericRepoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceRequest>()), Times.Never());
//            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
//            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Never());
//            _rabbitMQProducerMock.Verify(r => r.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//        }

//        [Fact]
//        public async Task Handle_InvalidRequestData_ReturnsError()
//        {
//            // Arrange
//            AssignTechnicianCommand command = null;

//            // Act
//            var result = await _handler.Handle(command, CancellationToken.None);

//            // Assert
//            Assert.False(result.Succeeded);
//            Assert.Equal("Request data is null", result.Message);
//            Assert.Null(result.Data);
//            _roleRepoMock.Verify(r => r.GetTechnicianRoleIdAsync(), Times.Never());
//            _userRoleRepoMock.Verify(r => r.GetTechniciansAsync(It.IsAny<string>()), Times.Never());
//            _genericRepoMock.Verify(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>(), It.IsAny<Expression<Func<MaintenanceRequest, object>>[]>()), Times.Never());
//            _genericRepoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceRequest>()), Times.Never());
//            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
//            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Never());
//            _rabbitMQProducerMock.Verify(r => r.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//        }

//        [Fact]
//        public async Task Handle_ImageUploadFails_ReturnsSuccess()
//        {
//            // Arrange
//            var requestDto = new MaintenanceRequestCommandDTO
//            {
//                CustomerId = "customer1",
//                PriorityLevel = 1,
//                CategoryId = Guid.NewGuid(),
//                RoomId = Guid.NewGuid(),
//                //Description = "Test request"
//            };
//            var imageMock = new Mock<IFormFile>(); // Mock ảnh
//            var command = new AssignTechnicianCommand(requestDto) { Image = new List<IFormFile> { imageMock.Object } };

//            // Mock role repository
//            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");

//            // Mock user role repository
//            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id"))
//                .ReturnsAsync(new List<string> { "tech1", "tech2" });

//            // Mock Get<MaintenanceRequest> with empty list
//            var maintenanceRequests = new List<MaintenanceRequest>();
//            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>(), It.IsAny<Expression<Func<MaintenanceRequest, object>>[]>()))
//                .Returns(maintenanceRequests.AsQueryable());
//            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(null, It.IsAny<Expression<Func<MaintenanceRequest, object>>[]>()))
//                .Returns(maintenanceRequests.AsQueryable());

//            // Mock AddAsync for MaintenanceRequest
//            Guid generatedId = Guid.NewGuid();
//            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
//                .Callback<MaintenanceRequest>(mr => mr.Id = generatedId)
//                .Returns(Task.CompletedTask);

//            // Mock SaveChangesAsync and transaction
//            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
//            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(new Mock<IDbContextTransaction>().Object);
//            _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>())).Returns(Task.CompletedTask);

//            // Mock RabbitMQ publish
//            _rabbitMQProducerMock.Setup(r => r.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
//                .Returns(Task.CompletedTask);

//            // Mock Cloudinary upload to throw exception
//            _cloudinaryServiceMock.Setup(c => c.UploadPhotoAsync(It.IsAny<IFormFile>()))
//                .ThrowsAsync(new Exception("Failed to upload image"));

//            // Act
//            var result = await _handler.Handle(command, CancellationToken.None);

//            // Assert
//            Assert.True(result.Succeeded);
//            Assert.Equal("Confirm request was successful", result.Message);
//            Assert.NotNull(result.Data);
//            Assert.Equal(generatedId, result.Data.requestId);
//            Assert.Equal("tech1", result.Data.TechnicianId);
//            Assert.Equal("Pending", result.Data.Status);
//            Assert.Equal(requestDto.Description, result.Data.Description);
//            Assert.Equal(requestDto.CustomerId, result.Data.CustomerId);
//            Assert.Equal(requestDto.PriorityLevel, result.Data.PriorityLevel);
//            Assert.Equal(requestDto.CategoryId, result.Data.CategoryId);
//            Assert.Equal(requestDto.RoomId, result.Data.RoomId);
//            _genericRepoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceRequest>()), Times.Once());
//            _genericRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<MaintenanceRequestImage>>()), Times.Never());
//            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once()); // Chỉ gọi cho MaintenanceRequest
//            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Once());
//            _rabbitMQProducerMock.Verify(r => r.PublishAsync(It.IsAny<object>(), "maintenance_exchange", "maintenance_request"), Times.Once());
//            _cloudinaryServiceMock.Verify(c => c.UploadPhotoAsync(It.IsAny<IFormFile>()), Times.Once());
//        }


//        //[Fact]
//        //public async Task Handle_TechnicianRoleNotFound_ThrowsException()
//        //{
//        //    // Arrange
//        //    var requestDto = new MaintenanceRequestCommandDTO
//        //    {
//        //        CustomerId = "customer1",
//        //        PriorityLevel = 1,
//        //        CategoryId = Guid.NewGuid(),
//        //        RoomId = Guid.NewGuid()
//        //    };
//        //    var command = new AssignTechnicianCommand(requestDto);

//        //    _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync((string)null);

//        //    // Act
//        //    var result = await _handler.Handle(command, CancellationToken.None);

//        //    // Assert
//        //    Assert.False(result.Succeeded);
//        //    Assert.Contains("Technician role does not exist", result.Message);
//        //    _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Once());
//        //}

//        //[Fact]
//        //public async Task Handle_InvalidRequestData_ReturnsError()
//        //{
//        //    // Arrange
//        //    AssignTechnicianCommand command = null;
//        //    try
//        //    {
//        //        command = new AssignTechnicianCommand(null); // Should throw ArgumentNullException
//        //        Assert.False(true, "Expected ArgumentNullException was not thrown.");
//        //    }
//        //    catch (ArgumentNullException ex)
//        //    {
//        //        Assert.Equal("requestDto", ex.ParamName);
//        //    }

//        //    // Act
//        //    var result = await _handler.Handle(command, CancellationToken.None);

//        //    // Assert
//        //    Assert.False(result.Succeeded);
//        //    Assert.Equal("Request data is null", result.Message);
//        //    _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never());
//        //}

//        //[Fact]
//        //public async Task Handle_ImageUploadFails_StillReturnsSuccess()
//        //{
//        //    // Arrange
//        //    var requestDto = new MaintenanceRequestCommandDTO
//        //    {
//        //        CustomerId = "customer1",
//        //        PriorityLevel = 1,
//        //        CategoryId = Guid.NewGuid(),
//        //        RoomId = Guid.NewGuid(),
//        //        Description = "Test request"
//        //    };
//        //    var images = new List<IFormFile> { new Mock<IFormFile>().Object };
//        //    var command = new AssignTechnicianCommand(requestDto, images);

//        //    _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
//        //    _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1" });

//        //    _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
//        //        .Returns(new List<MaintenanceRequest>().AsQueryable());
//        //    _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
//        //        .Callback<MaintenanceRequest>(mr => mr.Id = Guid.NewGuid());

//        //    _cloudinaryServiceMock.Setup(c => c.UploadPhotoAsync(It.IsAny<IFormFile>()))
//        //        .ThrowsAsync(new Exception("Upload failed"));

//        //    // Act
//        //    var result = await _handler.Handle(command, CancellationToken.None);

//        //    // Assert
//        //    Assert.True(result.Succeeded);
//        //    Assert.Equal("Confirm request was successful", result.Message);
//        //    _loggerMock.Verify(l => l.Log(
//        //        LogLevel.Warning,
//        //        It.IsAny<EventId>(),
//        //        It.IsAny<It.IsAnyType>(),
//        //        It.IsAny<Exception>(),
//        //        It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once());
//        //    _genericRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<MaintenanceRequestImage>>()), Times.Never());
//        //}

//        [Fact]
//        public async Task Handle_RabbitMQPublishFails_StillReturnsSuccess()
//        {
//            // Arrange
//            var requestDto = new MaintenanceRequestCommandDTO
//            {
//                CustomerId = "customer1",
//                PriorityLevel = 1,
//                CategoryId = Guid.NewGuid(),
//                RoomId = Guid.NewGuid(),
//                Description = "Test request"
//            };
//            var command = new AssignTechnicianCommand(requestDto);

//            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
//            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1" });

//            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
//                .Returns(new List<MaintenanceRequest>().AsQueryable());
//            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
//                .Callback<MaintenanceRequest>(mr => mr.Id = Guid.NewGuid());

//            _rabbitMQProducerMock.Setup(r => r.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ThrowsAsync(new Exception("RabbitMQ publish failed"));

//            // Act
//            var result = await _handler.Handle(command, CancellationToken.None);

//            // Assert
//            Assert.True(result.Succeeded);
//            Assert.Equal("Confirm request was successful", result.Message);
//            _loggerMock.Verify(l => l.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.IsAny<It.IsAnyType>(),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once());
//        }

//        [Fact]
//        public async Task Handle_MultipleTechnicians_AssignsRoundRobin()
//        {
//            // Arrange
//            var requestDto = new MaintenanceRequestCommandDTO
//            {
//                CustomerId = "customer1",
//                PriorityLevel = 1,
//                CategoryId = Guid.NewGuid(),
//                RoomId = Guid.NewGuid(),
//                Description = "Test request"
//            };
//            var command = new AssignTechnicianCommand(requestDto);

//            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
//            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1", "tech2" });

//            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
//                .Returns(new List<MaintenanceRequest>().AsQueryable());
//            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
//                .Callback<MaintenanceRequest>(mr => mr.Id = Guid.NewGuid());

//            // Act
//            var result1 = await _handler.Handle(command, CancellationToken.None);
//            var result2 = await _handler.Handle(command, CancellationToken.None);

//            // Assert
//            Assert.True(result1.Succeeded);
//            Assert.True(result2.Succeeded);
//            Assert.Equal("tech1", result1.Data.TechnicianId);
//            Assert.Equal("tech2", result2.Data.TechnicianId);
//        }

//        [Fact]
//        public async Task Handle_ConcurrentRequests_AssignsTechniciansSafely()
//        {
//            // Arrange
//            var requestDto = new MaintenanceRequestCommandDTO
//            {
//                CustomerId = "customer1",
//                PriorityLevel = 1,
//                CategoryId = Guid.NewGuid(),
//                RoomId = Guid.NewGuid(),
//                Description = "Test request"
//            };
//            var command = new AssignTechnicianCommand(requestDto);

//            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
//            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1", "tech2" });
//            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
//                .Returns(new List<MaintenanceRequest>().AsQueryable());
//            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
//                .Callback<MaintenanceRequest>(mr => mr.Id = Guid.NewGuid());

//            // Act
//            var tasks = Enumerable.Range(0, 10).Select(_ => _handler.Handle(command, CancellationToken.None)).ToList();
//            var results = await Task.WhenAll(tasks);

//            // Assert
//            var technicianIds = results.Select(r => r.Data.TechnicianId).Distinct().ToList();
//            Assert.Equal(2, technicianIds.Count); // Only tech1 and tech2 should be assigned
//            Assert.Contains("tech1", technicianIds);
//            Assert.Contains("tech2", technicianIds);
//        }
//    }
//}


////using EffiAP.Application.Commands.MaintainRequestCommand;
////using EffiAP.Application.Handlers.MaintainRequestHandler;
////using EffiRent.Application.Services.Rabbit;
////using EffiAP.Application.Services.Upload.Cloudinary;
////using EffiAP.Application.Wrappers;
////using EffiAP.Domain.Entities;
////using EffiAP.Domain.Models;
////using EffiAP.Domain.ViewModels.MaintainRequest;
////using EffiAP.Infrastructure.IRepositories;
////using MediatR;
////using Microsoft.AspNetCore.Http;
////using Microsoft.EntityFrameworkCore.Infrastructure;
////using Microsoft.EntityFrameworkCore.Storage;
////using Microsoft.Extensions.Logging;
////using Moq;
////using System;
////using System.Collections.Generic;
////using System.Linq;
////using System.Linq.Expressions;
////using System.Threading;
////using System.Threading.Tasks;
////using Xunit;

////namespace EffiAP.Application.Tests.Handlers
////{
////    public class AssignTechnicianCommandHandlerTests
////    {
////        private readonly Mock<IApplicationRoleRepository> _roleRepoMock;
////        private readonly Mock<IApplicationUserRoleRepository> _userRoleRepoMock;
////        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
////        private readonly Mock<IGenericRepository> _genericRepoMock;
////        private readonly Mock<IRabbitMQProducerService> _rabbitMQProducerMock;
////        private readonly Mock<ICloudinaryService> _cloudinaryServiceMock;
////        private readonly Mock<ILogger<AssignTechnicianCommandHandler>> _loggerMock;
////        private readonly AssignTechnicianCommandHandler _handler;

////        public AssignTechnicianCommandHandlerTests()
////        {
////            _roleRepoMock = new Mock<IApplicationRoleRepository>();
////            _userRoleRepoMock = new Mock<IApplicationUserRoleRepository>();
////            _unitOfWorkMock = new Mock<IUnitOfWork>();
////            _genericRepoMock = new Mock<IGenericRepository>();
////            _rabbitMQProducerMock = new Mock<IRabbitMQProducerService>();
////            _cloudinaryServiceMock = new Mock<ICloudinaryService>();
////            _loggerMock = new Mock<ILogger<AssignTechnicianCommandHandler>>();

////            // Setup IUnitOfWork to return IGenericRepository
////            _unitOfWorkMock.Setup(u => u.Repository).Returns(_genericRepoMock.Object);
////            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(new Mock<IDbContextTransaction>().Object);
////            _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>())).Returns(Task.CompletedTask);
////            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<IDbContextTransaction>())).Returns(Task.CompletedTask);
////            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

////            _handler = new AssignTechnicianCommandHandler(
////                _roleRepoMock.Object,
////                _userRoleRepoMock.Object,
////                _unitOfWorkMock.Object,
////                _rabbitMQProducerMock.Object,
////                _cloudinaryServiceMock.Object,
////                _loggerMock.Object);
////        }

////        [Fact]
////        public async Task Handle_ValidRequestWithAvailableTechnician_ReturnsSuccess()
////        {
////            // Arrange
////            var command = new AssignTechnicianCommand
////            {
////                RequestDto = new MaintenanceRequestCommandDTO
////                {
////                    CustomerId = "customer1",
////                    PriorityLevel = 1,
////                    CategoryId = Guid.NewGuid(),
////                    RoomId = Guid.NewGuid(),
////                    Description = "Test request"
////                }
////            };

////            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
////            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1", "tech2" });

////            var maintenanceRequests = new List<MaintenanceRequest>();
////            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(maintenanceRequests.AsQueryable());

////            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Callback<MaintenanceRequest>(mr => mr.Id = Guid.NewGuid());

////            // Act
////            var result = await _handler.Handle(command, CancellationToken.None);

////            // Assert
////            Assert.True(result.Succeeded);
////            Assert.NotNull(result.Data);
////            Assert.NotEqual(Guid.Empty, result.Data.requestId);
////            Assert.Equal("tech1", result.Data.TechnicianId);
////            Assert.Equal("Pending", result.Data.Status);
////            Assert.Equal(command.RequestDto.Description, result.Data.Description);
////            _rabbitMQProducerMock.Verify(r => r.PublishAsync(It.IsAny<object>(), "maintenance_exchange", "maintenance_request"), Times.Once());
////            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Once());
////        }

////        [Fact]
////        public async Task Handle_NoAvailableTechnicians_QueuesRequest()
////        {
////            // Arrange
////            var command = new AssignTechnicianCommand
////            {
////                RequestDto = new MaintenanceRequestCommandDTO
////                {
////                    CustomerId = "customer1",
////                    PriorityLevel = 1,
////                    CategoryId = Guid.NewGuid(),
////                    RoomId = Guid.NewGuid(),
////                    Description = "Test request"
////                }
////            };

////            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
////            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1" });

////            var maintenanceRequests = new List<MaintenanceRequest>
////            {
////                new MaintenanceRequest { TechnicianId = "tech1", Status = "Pending" }
////            };
////            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(maintenanceRequests.AsQueryable());

////            // Act
////            var result = await _handler.Handle(command, CancellationToken.None);

////            // Assert
////            Assert.False(result.Succeeded);
////            Assert.Equal("No available technicians; request has been queued.", result.Message);
////            _rabbitMQProducerMock.Verify(r => r.PublishAsync(
////                It.Is<object>(m => m.ToString().Contains("Queued")),
////                "maintenance_exchange",
////                "maintenance_request"), Times.Once());
////            _genericRepoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceRequest>()), Times.Never());
////        }

////        [Fact]
////        public async Task Handle_TechnicianRoleNotFound_ThrowsException()
////        {
////            // Arrange
////            var command = new AssignTechnicianCommand
////            {
////                RequestDto = new MaintenanceRequestCommandDTO
////                {
////                    CustomerId = "customer1",
////                    PriorityLevel = 1,
////                    CategoryId = Guid.NewGuid(),
////                    RoomId = Guid.NewGuid()
////                }
////            };

////            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync((string)null);

////            // Act
////            var result = await _handler.Handle(command, CancellationToken.None);

////            // Assert
////            Assert.False(result.Succeeded);
////            Assert.Contains("Technician role does not exist", result.Message);
////            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Once());
////        }

////        [Fact]
////        public async Task Handle_InvalidRequestData_ReturnsError()
////        {
////            // Arrange
////            var command = new AssignTechnicianCommand { RequestDto = null };

////            // Act
////            var result = await _handler.Handle(command, CancellationToken.None);

////            // Assert
////            Assert.False(result.Succeeded);
////            Assert.Equal("Request data is null", result.Message);
////            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never());
////        }

////        [Fact]
////        public async Task Handle_ImageUploadFails_StillReturnsSuccess()
////        {
////            // Arrange
////            var command = new AssignTechnicianCommand
////            {
////                RequestDto = new MaintenanceRequestCommandDTO
////                {
////                    CustomerId = "customer1",
////                    PriorityLevel = 1,
////                    CategoryId = Guid.NewGuid(),
////                    RoomId = Guid.NewGuid(),
////                    Description = "Test request"
////                },
////                Image = new List<IFormFile> { new Mock<IFormFile>().Object }
////            };

////            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
////            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1" });

////            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(new List<MaintenanceRequest>().AsQueryable());
////            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Callback<MaintenanceRequest>(mr => mr.Id = Guid.NewGuid());

////            _cloudinaryServiceMock.Setup(c => c.UploadPhotoAsync(It.IsAny<IFormFile>()))
////                .ThrowsAsync(new Exception("Upload failed"));

////            // Act
////            var result = await _handler.Handle(command, CancellationToken.None);

////            // Assert
////            Assert.True(result.Succeeded);
////            Assert.Equal("Confirm request was successful", result.Message);
////            _loggerMock.Verify(l => l.Log(
////                LogLevel.Warning,
////                It.IsAny<EventId>(),
////                It.IsAny<It.IsAnyType>(),
////                It.IsAny<Exception>(),
////                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once());
////            _genericRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<MaintenanceRequestImage>>()), Times.Never());
////        }

////        [Fact]
////        public async Task Handle_RabbitMQPublishFails_StillReturnsSuccess()
////        {
////            // Arrange
////            var command = new AssignTechnicianCommand
////            {
////                RequestDto = new MaintenanceRequestCommandDTO
////                {
////                    CustomerId = "customer1",
////                    PriorityLevel = 1,
////                    CategoryId = Guid.NewGuid(),
////                    RoomId = Guid.NewGuid(),
////                    Description = "Test request"
////                }
////            };

////            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
////            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1" });

////            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(new List<MaintenanceRequest>().AsQueryable());
////            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Callback<MaintenanceRequest>(mr => mr.Id = Guid.NewGuid());

////            _rabbitMQProducerMock.Setup(r => r.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
////                .ThrowsAsync(new Exception("RabbitMQ publish failed"));

////            // Act
////            var result = await _handler.Handle(command, CancellationToken.None);

////            // Assert
////            Assert.True(result.Succeeded);
////            Assert.Equal("Confirm request was successful", result.Message);
////            _loggerMock.Verify(l => l.Log(
////                LogLevel.Error,
////                It.IsAny<EventId>(),
////                It.IsAny<It.IsAnyType>(),
////                It.IsAny<Exception>(),
////                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once());
////        }

////        [Fact]
////        public async Task Handle_MultipleTechnicians_AssignsRoundRobin()
////        {
////            // Arrange
////            var command = new AssignTechnicianCommand
////            {
////                RequestDto = new MaintenanceRequestCommandDTO
////                {
////                    CustomerId = "customer1",
////                    PriorityLevel = 1,
////                    CategoryId = Guid.NewGuid(),
////                    RoomId = Guid.NewGuid(),
////                    Description = "Test request"
////                }
////            };

////            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
////            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1", "tech2" });

////            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(new List<MaintenanceRequest>().AsQueryable());
////            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Callback<MaintenanceRequest>(mr => mr.Id = Guid.NewGuid());

////            // Act
////            var result1 = await _handler.Handle(command, CancellationToken.None);
////            var result2 = await _handler.Handle(command, CancellationToken.None);

////            // Assert
////            Assert.True(result1.Succeeded);
////            Assert.True(result2.Succeeded);
////            Assert.Equal("tech1", result1.Data.TechnicianId);
////            Assert.Equal("tech2", result2.Data.TechnicianId);
////        }

////        [Fact]
////        public async Task Handle_ConcurrentRequests_AssignsTechniciansSafely()
////        {
////            // Arrange
////            var command = new AssignTechnicianCommand
////            {
////                RequestDto = new MaintenanceRequestCommandDTO
////                {
////                    CustomerId = "customer1",
////                    PriorityLevel = 1,
////                    CategoryId = Guid.NewGuid(),
////                    RoomId = Guid.NewGuid(),
////                    Description = "Test request"
////                }
////            };

////            _roleRepoMock.Setup(r => r.GetTechnicianRoleIdAsync()).ReturnsAsync("technician_role_id");
////            _userRoleRepoMock.Setup(r => r.GetTechniciansAsync("technician_role_id")).ReturnsAsync(new List<string> { "tech1", "tech2" });
////            _genericRepoMock.Setup(r => r.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(new List<MaintenanceRequest>().AsQueryable());
////            _genericRepoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Callback<MaintenanceRequest>(mr => mr.Id = Guid.NewGuid());

////            // Act
////            var tasks = Enumerable.Range(0, 10).Select(_ => _handler.Handle(command, CancellationToken.None)).ToList();
////            var results = await Task.WhenAll(tasks);

////            // Assert
////            var technicianIds = results.Select(r => r.Data.TechnicianId).Distinct().ToList();
////            Assert.Equal(2, technicianIds.Count); // Only tech1 and tech2 should be assigned
////            Assert.Contains("tech1", technicianIds);
////            Assert.Contains("tech2", technicianIds);
////        }
////    }
////}

////using EffiAP.Application.Commands.MaintainRequestCommand;
////using EffiAP.Application.Handlers.MaintainRequestHandler;
////using EffiAP.Application.Services.Rabbit;
////using EffiAP.Application.Services.Upload.Cloudinary;
////using EffiAP.Application.Wrappers;
////using EffiAP.Domain.Entities;
////using EffiAP.Domain.ViewModels.MaintainRequest;
////using EffiAP.Infrastructure.IRepositories;
////using Microsoft.EntityFrameworkCore.Storage;
////using Microsoft.Extensions.Logging;
////using Moq;
////using System;
////using System.Collections.Generic;
////using System.Linq;
////using System.Linq.Expressions;
////using System.Threading;
////using System.Threading.Tasks;
////using Xunit;
////using FluentAssertions;
////using EffiAP.Domain.Models;
////using Microsoft.AspNetCore.Http;
////using EffiRent.Application.Services.Rabbit;
////using Moq.EntityFrameworkCore;
////using Microsoft.EntityFrameworkCore;
////using System.Data.Entity.Infrastructure;
////using NPoco.Linq;



////namespace EffiAP.Application.Tests.Unit.Handlers.MaintainRequestHandler
////{
////    public class AssignTechnicianCommandHandlerTests
////    {
////        private readonly Mock<IApplicationRoleRepository> _applicationRoleRepositoryMock;
////        private readonly Mock<IApplicationUserRoleRepository> _applicationUserRoleRepositoryMock;
////        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
////        private readonly Mock<IRabbitMQProducerService> _rabbitMQProducerMock;
////        private readonly Mock<ICloudinaryService> _cloudinaryServiceMock;
////        private readonly Mock<ILogger<AssignTechnicianCommandHandler>> _loggerMock;
////        private readonly AssignTechnicianCommandHandler _handler;

////        public AssignTechnicianCommandHandlerTests()
////        {
////            _applicationRoleRepositoryMock = new Mock<IApplicationRoleRepository>();
////            _applicationUserRoleRepositoryMock = new Mock<IApplicationUserRoleRepository>();
////            _unitOfWorkMock = new Mock<IUnitOfWork>();
////            _rabbitMQProducerMock = new Mock<IRabbitMQProducerService>();
////            _cloudinaryServiceMock = new Mock<ICloudinaryService>();
////            _loggerMock = new Mock<ILogger<AssignTechnicianCommandHandler>>();

////            // Mock Repository property
////            var repositoryMock = new Mock<IGenericRepository>();
////            _unitOfWorkMock.Setup(uow => uow.Repository).Returns(repositoryMock.Object);

////            _handler = new AssignTechnicianCommandHandler(
////                _applicationRoleRepositoryMock.Object,
////                _applicationUserRoleRepositoryMock.Object,
////                _unitOfWorkMock.Object,
////                _rabbitMQProducerMock.Object,
////                _cloudinaryServiceMock.Object,
////                _loggerMock.Object);
////        }

////        [Fact]
////        public async Task Handle_WhenRequestIsNull_ReturnsFailedResponse()
////        {
////            // Arrange
////            AssignTechnicianCommand request = null;

////            // Act
////            var result = await _handler.Handle(request, CancellationToken.None);

////            // Assert
////            result.Succeeded.Should().BeFalse();
////            result.Message.Should().Be("Request data is null");
////            _loggerMock.VerifyLog(LogLevel.Warning, "Request or RequestDto is null.", Times.Once());
////        }

////        [Fact]
////        public async Task Handle_WhenRequestDtoIsNull_ReturnsFailedResponse()
////        {
////            // Arrange
////            var request = new AssignTechnicianCommand(null, null);

////            // Act
////            var result = await _handler.Handle(request, CancellationToken.None);

////            // Assert
////            result.Succeeded.Should().BeFalse();
////            result.Message.Should().Be("Request data is null");
////            _loggerMock.VerifyLog(LogLevel.Warning, "Request or RequestDto is null.", Times.Once());
////        }

////        [Fact]
////        public async Task Handle_WhenTechnicianRoleDoesNotExist_ThrowsException()
////        {
////            // Arrange
////            var requestDto = new MaintenanceRequestCommandDTO
////            {
////                CustomerId = Guid.NewGuid().ToString(),
////                PriorityLevel = 1,
////                CategoryId = Guid.NewGuid(),
////                RoomId = Guid.NewGuid()
////            };
////            var request = new AssignTechnicianCommand(requestDto, null);
////            _applicationRoleRepositoryMock.Setup(repo => repo.GetTechnicianRoleIdAsync())
////                .ReturnsAsync((string)null);

////            // Act & Assert
////            // Act
////            var result = await _handler.Handle(request, CancellationToken.None);

////            // Assert
////            result.Succeeded.Should().BeFalse();
////            result.Message.Should().Be("Technician role does not exist.");
////            result.Data.Should().BeNull();
////            _loggerMock.VerifyLog(LogLevel.Error, "Technician role does not exist.", Times.Once());
////            _applicationRoleRepositoryMock.Verify(repo => repo.GetTechnicianRoleIdAsync(), Times.Once());
////            //await Assert.ThrowsAsync<Exception>(() => _handler.Handle(request, CancellationToken.None));
////            //_loggerMock.VerifyLog(LogLevel.Error, "Technician role does not exist.", Times.Once());
////        }


////        [Fact]
////        public async Task Handle_WhenNoAvailableTechnicians_QueuesRequestAndReturnsFailedResponse()
////        {
////            // Arrange
////            var requestDto = new MaintenanceRequestCommandDTO
////            {
////                CustomerId = Guid.NewGuid().ToString(),
////                PriorityLevel = 1,
////                CategoryId = Guid.NewGuid(),
////                RoomId = Guid.NewGuid()
////            };
////            var request = new AssignTechnicianCommand(requestDto, null);
////            var technicianRole = "technician-role-id";
////            var technicianUserIds = new List<string> { "tech1", "tech2" };

////            _applicationRoleRepositoryMock.Setup(repo => repo.GetTechnicianRoleIdAsync())
////                .ReturnsAsync(technicianRole);
////            _applicationUserRoleRepositoryMock.Setup(repo => repo.GetTechniciansAsync(technicianRole))
////                .ReturnsAsync(technicianUserIds);

////            // Tạo danh sách dữ liệu
////            var maintenanceRequests = new List<MaintenanceRequest>
////    {
////        new MaintenanceRequest { TechnicianId = "tech1", Status = "Pending" },
////        new MaintenanceRequest { TechnicianId = "tech2", Status = "Pending" }
////    };

////            // Mock DbSet<MaintenanceRequest> với Moq.EntityFrameworkCore
////            var dbSetMock = new Mock<DbSet<MaintenanceRequest>>();
////            dbSetMock.As<IDbAsyncEnumerable<MaintenanceRequest>>()
////                .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
////                .Returns(new AsyncEnumerator<MaintenanceRequest>(maintenanceRequests.GetEnumerator()));
////            dbSetMock.As<IQueryable<MaintenanceRequest>>()
////                .Setup(m => m.Provider)
////                .Returns(new AsyncQueryProvider<MaintenanceRequest>(maintenanceRequests.AsQueryable().Provider));
////            dbSetMock.As<IQueryable<MaintenanceRequest>>()
////                .Setup(m => m.Expression)
////                .Returns(maintenanceRequests.AsQueryable().Expression);
////            dbSetMock.As<IQueryable<MaintenanceRequest>>()
////                .Setup(m => m.ElementType)
////                .Returns(maintenanceRequests.AsQueryable().ElementType);
////            dbSetMock.As<IQueryable<MaintenanceRequest>>()
////                .Setup(m => m.GetEnumerator())
////                .Returns(maintenanceRequests.AsQueryable().GetEnumerator());

////            // Mock Get<MaintenanceRequest> để trả về IQueryable từ DbSet
////            _unitOfWorkMock.Setup(uow => uow.Repository.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(dbSetMock.Object.AsQueryable());

////            _rabbitMQProducerMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
////                .Returns(Task.CompletedTask);

////            // Act
////            var result = await _handler.Handle(request, CancellationToken.None);

////            // Assert
////            result.Succeeded.Should().BeFalse();
////            result.Message.Should().Be("No available technicians; request has been queued.");
////            result.Data.Should().BeEquivalentTo(requestDto);
////            _rabbitMQProducerMock.Verify(
////                x => x.PublishAsync(It.IsAny<object>(), "maintenance_exchange", "maintenance_request"),
////                Times.Once());
////            _loggerMock.VerifyLog(LogLevel.Warning, "No available technicians found for assignment. Queuing request.", Times.Once());
////            _loggerMock.VerifyLog(LogLevel.Information, "Published queued request to maintenance_exchange", Times.Once());
////        }

////        //[Fact]
////        //public async Task Handle_WhenNoAvailableTechnicians_QueuesRequestAndReturnsFailedResponse()
////        //{
////        //    // Arrange
////        //    var requestDto = new MaintenanceRequestCommandDTO
////        //    {
////        //        CustomerId = Guid.NewGuid().ToString(),
////        //        PriorityLevel = 1,
////        //        CategoryId = Guid.NewGuid(),
////        //        RoomId = Guid.NewGuid()
////        //    };
////        //    var request = new AssignTechnicianCommand(requestDto, null);
////        //    var technicianRole = "technician-role-id";
////        //    var technicianUserIds = new List<string> { "tech1", "tech2" };
////        //    _applicationRoleRepositoryMock.Setup(repo => repo.GetTechnicianRoleIdAsync())
////        //        .ReturnsAsync(technicianRole);
////        //    _applicationUserRoleRepositoryMock.Setup(repo => repo.GetTechniciansAsync(technicianRole))
////        //        .ReturnsAsync(technicianUserIds);
////        //    _unitOfWorkMock.Setup(uow => uow.Repository.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////        //        .Returns(new List<MaintenanceRequest>
////        //        {
////        //            new MaintenanceRequest { TechnicianId = "tech1", Status = "Pending" },
////        //            new MaintenanceRequest { TechnicianId = "tech2", Status = "Pending" }
////        //        }.AsQueryable());

////        //    // Act
////        //    var result = await _handler.Handle(request, CancellationToken.None);

////        //    // Assert
////        //    result.Succeeded.Should().BeFalse();
////        //    result.Message.Should().Be("No available technicians; request has been queued.");
////        //    result.Data.Should().BeEquivalentTo(requestDto);
////        //    _rabbitMQProducerMock.Verify(
////        //        x => x.PublishAsync(It.IsAny<object>(), "maintenance_exchange", "maintenance_request"),
////        //        Times.Once());
////        //    _loggerMock.VerifyLog(LogLevel.Warning, "No available technicians found for assignment. Queuing request.", Times.Once());
////        //    _loggerMock.VerifyLog(LogLevel.Information, "Published queued request to maintenance_exchange", Times.Once());
////        //}

////        [Fact]
////        public async Task Handle_WhenTechnicianAvailable_AssignsTechnicianAndSavesRequest()
////        {
////            // Arrange
////            var requestDto = new MaintenanceRequestCommandDTO
////            {
////                CustomerId = Guid.NewGuid().ToString(),
////                PriorityLevel = 1,
////                CategoryId = Guid.NewGuid(),
////                RoomId = Guid.NewGuid()
////            };
////            var request = new AssignTechnicianCommand(requestDto, null);
////            var technicianRole = "technician-role-id";
////            var technicianUserIds = new List<string> { "tech1" };
////            var maintenanceRequestId = Guid.NewGuid();
////            var transactionMock = new Mock<IDbContextTransaction>();
////            _applicationRoleRepositoryMock.Setup(repo => repo.GetTechnicianRoleIdAsync())
////                .ReturnsAsync(technicianRole);
////            _applicationUserRoleRepositoryMock.Setup(repo => repo.GetTechniciansAsync(technicianRole))
////                .ReturnsAsync(technicianUserIds);
////            _unitOfWorkMock.Setup(uow => uow.Repository.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(new List<MaintenanceRequest>().AsQueryable());
////            _unitOfWorkMock.Setup(uow => uow.BeginTransactionAsync())
////                .ReturnsAsync(transactionMock.Object);
////            _unitOfWorkMock.Setup(uow => uow.Repository.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Callback<MaintenanceRequest>(req => req.Id = maintenanceRequestId);
////            _unitOfWorkMock.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
////                .ReturnsAsync(1);
////            _unitOfWorkMock.Setup(uow => uow.CommitTransactionAsync(transactionMock.Object))
////                .Returns(Task.CompletedTask);
////            _rabbitMQProducerMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
////                .Returns(Task.CompletedTask);

////            // Act
////            var result = await _handler.Handle(request, CancellationToken.None);

////            // Assert
////            result.Succeeded.Should().BeTrue();
////            result.Message.Should().Be("Confirm request was successful");
////            result.Data.Should().NotBeNull();
////            result.Data.requestId.Should().Be(maintenanceRequestId);
////            result.Data.TechnicianId.Should().Be("tech1");
////            result.Data.Status.Should().Be("Pending");
////            _unitOfWorkMock.Verify(uow => uow.Repository.AddAsync(It.IsAny<MaintenanceRequest>()), Times.Once());
////            _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
////            _unitOfWorkMock.Verify(uow => uow.CommitTransactionAsync(transactionMock.Object), Times.Once());
////            _rabbitMQProducerMock.Verify(
////                x => x.PublishAsync(It.IsAny<object>(), "maintenance_exchange", "maintenance_request"),
////                Times.Once());
////            _loggerMock.VerifyLog(LogLevel.Information, $"MaintenanceRequest {maintenanceRequestId} created and committed", Times.Once());
////            _loggerMock.VerifyLog(LogLevel.Information, $"Published RabbitMQ message for MaintenanceRequest {maintenanceRequestId}", Times.Once());
////        }

////        [Fact]
////        public async Task Handle_WhenImagesProvided_UploadsAndSavesImages()
////        {
////            // Arrange
////            var requestDto = new MaintenanceRequestCommandDTO
////            {
////                CustomerId = Guid.NewGuid().ToString(),
////                PriorityLevel = 1,
////                CategoryId = Guid.NewGuid(),
////                RoomId = Guid.NewGuid()
////            };
////            var images = new List<IFormFile> { new Mock<IFormFile>().Object };
////            var request = new AssignTechnicianCommand(requestDto, images);
////            var technicianRole = "technician-role-id";
////            var technicianUserIds = new List<string> { "tech1" };
////            var maintenanceRequestId = Guid.NewGuid();
////            var transactionMock = new Mock<IDbContextTransaction>();
////            _applicationRoleRepositoryMock.Setup(repo => repo.GetTechnicianRoleIdAsync())
////                .ReturnsAsync(technicianRole);
////            _applicationUserRoleRepositoryMock.Setup(repo => repo.GetTechniciansAsync(technicianRole))
////                .ReturnsAsync(technicianUserIds);
////            _unitOfWorkMock.Setup(uow => uow.Repository.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(new List<MaintenanceRequest>().AsQueryable());
////            _unitOfWorkMock.Setup(uow => uow.BeginTransactionAsync())
////                .ReturnsAsync(transactionMock.Object);
////            _unitOfWorkMock.Setup(uow => uow.Repository.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Callback<MaintenanceRequest>(req => req.Id = maintenanceRequestId);
////            _unitOfWorkMock.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
////                .ReturnsAsync(1);
////            _unitOfWorkMock.Setup(uow => uow.CommitTransactionAsync(transactionMock.Object))
////                .Returns(Task.CompletedTask);
////            _cloudinaryServiceMock.Setup(x => x.UploadPhotoAsync(It.IsAny<IFormFile>()))
////                .ReturnsAsync("https://cloudinary.com/image.jpg");
////            _unitOfWorkMock.Setup(uow => uow.Repository.AddRangeAsync(It.IsAny<IEnumerable<MaintenanceRequestImage>>()))
////                .Returns(Task.CompletedTask);

////            // Act
////            var result = await _handler.Handle(request, CancellationToken.None);

////            // Assert
////            result.Succeeded.Should().BeTrue();
////            result.Data.requestId.Should().Be(maintenanceRequestId);
////            _cloudinaryServiceMock.Verify(x => x.UploadPhotoAsync(It.IsAny<IFormFile>()), Times.Once());
////            _unitOfWorkMock.Verify(uow => uow.Repository.AddRangeAsync(It.IsAny<IEnumerable<MaintenanceRequestImage>>()), Times.Once());
////            _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
////            _loggerMock.VerifyLog(LogLevel.Information, $"Saved 1 images for MaintenanceRequest {maintenanceRequestId}", Times.Once());
////        }

////        [Fact]
////        public async Task Handle_WhenImageUploadFails_ContinuesProcessing()
////        {
////            // Arrange
////            var requestDto = new MaintenanceRequestCommandDTO
////            {
////                CustomerId = Guid.NewGuid().ToString(),
////                PriorityLevel = 1,
////                CategoryId = Guid.NewGuid(),
////                RoomId = Guid.NewGuid()
////            };
////            var images = new List<IFormFile> { new Mock<IFormFile>().Object };
////            var request = new AssignTechnicianCommand(requestDto, images);
////            var technicianRole = "technician-role-id";
////            var technicianUserIds = new List<string> { "tech1" };
////            var maintenanceRequestId = Guid.NewGuid();
////            var transactionMock = new Mock<IDbContextTransaction>();
////            _applicationRoleRepositoryMock.Setup(repo => repo.GetTechnicianRoleIdAsync())
////                .ReturnsAsync(technicianRole);
////            _applicationUserRoleRepositoryMock.Setup(repo => repo.GetTechniciansAsync(technicianRole))
////                .ReturnsAsync(technicianUserIds);
////            _unitOfWorkMock.Setup(uow => uow.Repository.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(new List<MaintenanceRequest>().AsQueryable());
////            _unitOfWorkMock.Setup(uow => uow.BeginTransactionAsync())
////                .ReturnsAsync(transactionMock.Object);
////            _unitOfWorkMock.Setup(uow => uow.Repository.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Callback<MaintenanceRequest>(req => req.Id = maintenanceRequestId);
////            _unitOfWorkMock.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
////                .ReturnsAsync(1);
////            _unitOfWorkMock.Setup(uow => uow.CommitTransactionAsync(transactionMock.Object))
////                .Returns(Task.CompletedTask);
////            _cloudinaryServiceMock.Setup(x => x.UploadPhotoAsync(It.IsAny<IFormFile>()))
////                .ThrowsAsync(new Exception("Upload failed"));

////            // Act
////            var result = await _handler.Handle(request, CancellationToken.None);

////            // Assert
////            result.Succeeded.Should().BeTrue();
////            result.Data.requestId.Should().Be(maintenanceRequestId);
////            _cloudinaryServiceMock.Verify(x => x.UploadPhotoAsync(It.IsAny<IFormFile>()), Times.Once());
////            _unitOfWorkMock.Verify(uow => uow.Repository.AddRangeAsync(It.IsAny<IEnumerable<MaintenanceRequestImage>>()), Times.Never());
////            _loggerMock.VerifyLog(LogLevel.Warning, $"Failed to upload image for MaintenanceRequest {maintenanceRequestId}", Times.Once());
////        }

////        [Fact]
////        public async Task Handle_WhenSaveFails_RollsBackAndDeletesImages()
////        {
////            // Arrange
////            var requestDto = new MaintenanceRequestCommandDTO
////            {
////                CustomerId = Guid.NewGuid().ToString(),
////                PriorityLevel = 1,
////                CategoryId = Guid.NewGuid(),
////                RoomId = Guid.NewGuid()
////            };
////            var images = new List<IFormFile> { new Mock<IFormFile>().Object };
////            var request = new AssignTechnicianCommand(requestDto, images);
////            var technicianRole = "technician-role-id";
////            var technicianUserIds = new List<string> { "tech1" };
////            var transactionMock = new Mock<IDbContextTransaction>();
////            _applicationRoleRepositoryMock.Setup(repo => repo.GetTechnicianRoleIdAsync())
////                .ReturnsAsync(technicianRole);
////            _applicationUserRoleRepositoryMock.Setup(repo => repo.GetTechniciansAsync(technicianRole))
////                .ReturnsAsync(technicianUserIds);
////            _unitOfWorkMock.Setup(uow => uow.Repository.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(new List<MaintenanceRequest>().AsQueryable());
////            _unitOfWorkMock.Setup(uow => uow.BeginTransactionAsync())
////                .ReturnsAsync(transactionMock.Object);
////            _unitOfWorkMock.Setup(uow => uow.Repository.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Returns(Task.CompletedTask);
////            _unitOfWorkMock.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
////                .ThrowsAsync(new Exception("Database error"));
////            _cloudinaryServiceMock.Setup(x => x.UploadPhotoAsync(It.IsAny<IFormFile>()))
////                .ReturnsAsync("https://cloudinary.com/image.jpg");
////            _cloudinaryServiceMock.Setup(x => x.ExtractPublicId(It.IsAny<string>()))
////                .Returns("image-public-id");
////            _cloudinaryServiceMock.Setup(x => x.DeletePhotoAsync(It.IsAny<string>()))
////                .Returns(Task.CompletedTask);

////            // Act
////            var result = await _handler.Handle(request, CancellationToken.None);

////            // Assert
////            result.Succeeded.Should().BeFalse();
////            result.Message.Should().StartWith("Error assigning technician: Database error");
////            _unitOfWorkMock.Verify(uow => uow.RollbackTransactionAsync(transactionMock.Object), Times.Once());
////            _cloudinaryServiceMock.Verify(x => x.DeletePhotoAsync("image-public-id"), Times.Once());
////            _loggerMock.VerifyLog(LogLevel.Error, $"Failed to process AssignTechnicianCommand for CustomerId {requestDto.CustomerId}", Times.Once());
////            _loggerMock.VerifyLog(LogLevel.Information, "Deleted image https://cloudinary.com/image.jpg from Cloudinary", Times.Once());
////        }

////        [Fact]
////        public async Task Handle_WhenCancellationRequested_ThrowsOperationCanceledException()
////        {
////            // Arrange
////            var requestDto = new MaintenanceRequestCommandDTO
////            {
////                CustomerId = Guid.NewGuid().ToString(),
////                PriorityLevel = 1,
////                CategoryId = Guid.NewGuid(),
////                RoomId = Guid.NewGuid()
////            };
////            var request = new AssignTechnicianCommand(requestDto, null);
////            var cts = new CancellationTokenSource();
////            cts.Cancel();

////            // Act & Assert
////            await Assert.ThrowsAsync<OperationCanceledException>(() => _handler.Handle(request, cts.Token));
////        }

////        [Fact]
////        public async Task Handle_AssignsTechniciansInRoundRobinOrder()
////        {
////            // Arrange
////            var requestDto = new MaintenanceRequestCommandDTO
////            {
////                CustomerId = Guid.NewGuid().ToString(),
////                PriorityLevel = 1,
////                CategoryId = Guid.NewGuid(),
////                RoomId = Guid.NewGuid()
////            };
////            var request = new AssignTechnicianCommand(requestDto, null);
////            var technicianRole = "technician-role-id";
////            var technicianUserIds = new List<string> { "tech1", "tech2", "tech3" };
////            var maintenanceRequestId = Guid.NewGuid();
////            var transactionMock = new Mock<IDbContextTransaction>();
////            _applicationRoleRepositoryMock.Setup(repo => repo.GetTechnicianRoleIdAsync())
////                .ReturnsAsync(technicianRole);
////            _applicationUserRoleRepositoryMock.Setup(repo => repo.GetTechniciansAsync(technicianRole))
////                .ReturnsAsync(technicianUserIds);
////            _unitOfWorkMock.Setup(uow => uow.Repository.Get<MaintenanceRequest>(It.IsAny<Expression<Func<MaintenanceRequest, bool>>>()))
////                .Returns(new List<MaintenanceRequest>().AsQueryable());
////            _unitOfWorkMock.Setup(uow => uow.BeginTransactionAsync())
////                .ReturnsAsync(transactionMock.Object);
////            _unitOfWorkMock.Setup(uow => uow.Repository.AddAsync(It.IsAny<MaintenanceRequest>()))
////                .Callback<MaintenanceRequest>(req => req.Id = maintenanceRequestId);
////            _unitOfWorkMock.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
////                .ReturnsAsync(1);
////            _unitOfWorkMock.Setup(uow => uow.CommitTransactionAsync(transactionMock.Object))
////                .Returns(Task.CompletedTask);
////            _rabbitMQProducerMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
////                .Returns(Task.CompletedTask);

////            // Act
////            var firstResult = await _handler.Handle(request, CancellationToken.None);
////            var secondResult = await _handler.Handle(request, CancellationToken.None);
////            var thirdResult = await _handler.Handle(request, CancellationToken.None);
////            var fourthResult = await _handler.Handle(request, CancellationToken.None);

////            // Assert
////            firstResult.Succeeded.Should().BeTrue("First result should succeed");
////            firstResult.Data.Should().NotBeNull("First result Data should not be null");
////            firstResult.Data.TechnicianId.Should().Be("tech1");

////            secondResult.Succeeded.Should().BeTrue("Second result should succeed");
////            secondResult.Data.Should().NotBeNull("Second result Data should not be null");
////            secondResult.Data.TechnicianId.Should().Be("tech2");

////            thirdResult.Succeeded.Should().BeTrue("Third result should succeed");
////            thirdResult.Data.Should().NotBeNull("Third result Data should not be null");
////            thirdResult.Data.TechnicianId.Should().Be("tech3");

////            fourthResult.Succeeded.Should().BeTrue("Fourth result should succeed");
////            fourthResult.Data.Should().NotBeNull("Fourth result Data should not be null");
////            fourthResult.Data.TechnicianId.Should().Be("tech1");
////        }

////        [Fact]
////        public void Constructor_WhenAnyDependencyIsNull_ThrowsArgumentNullException()
////        {
////            // Act & Assert
////            Assert.Throws<ArgumentNullException>(() => new AssignTechnicianCommandHandler(
////                null, _applicationUserRoleRepositoryMock.Object, _unitOfWorkMock.Object,
////                _rabbitMQProducerMock.Object, _cloudinaryServiceMock.Object, _loggerMock.Object));

////            Assert.Throws<ArgumentNullException>(() => new AssignTechnicianCommandHandler(
////                _applicationRoleRepositoryMock.Object, null, _unitOfWorkMock.Object,
////                _rabbitMQProducerMock.Object, _cloudinaryServiceMock.Object, _loggerMock.Object));

////            Assert.Throws<ArgumentNullException>(() => new AssignTechnicianCommandHandler(
////                _applicationRoleRepositoryMock.Object, _applicationUserRoleRepositoryMock.Object, null,
////                _rabbitMQProducerMock.Object, _cloudinaryServiceMock.Object, _loggerMock.Object));

////            Assert.Throws<ArgumentNullException>(() => new AssignTechnicianCommandHandler(
////                _applicationRoleRepositoryMock.Object, _applicationUserRoleRepositoryMock.Object, _unitOfWorkMock.Object,
////                null, _cloudinaryServiceMock.Object, _loggerMock.Object));

////            Assert.Throws<ArgumentNullException>(() => new AssignTechnicianCommandHandler(
////                _applicationRoleRepositoryMock.Object, _applicationUserRoleRepositoryMock.Object, _unitOfWorkMock.Object,
////                _rabbitMQProducerMock.Object, null, _loggerMock.Object));

////            Assert.Throws<ArgumentNullException>(() => new AssignTechnicianCommandHandler(
////                _applicationRoleRepositoryMock.Object, _applicationUserRoleRepositoryMock.Object, _unitOfWorkMock.Object,
////                _rabbitMQProducerMock.Object, _cloudinaryServiceMock.Object, null));
////        }
////    }
////}

////    public static class LoggerMockExtensions
////    {
////        public static void VerifyLog<T>(this Mock<ILogger<T>> loggerMock, LogLevel logLevel, string message, Times times)
////        {
////            loggerMock.Verify(
////                x => x.Log(
////                    logLevel,
////                    It.IsAny<EventId>(),
////                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(message)),
////                    It.IsAny<Exception>(),
////                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
////                times);
////        }
////    }
