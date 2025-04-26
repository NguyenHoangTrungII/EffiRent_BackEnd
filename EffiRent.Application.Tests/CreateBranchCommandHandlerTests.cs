using EffiAP.Application.Commands.BranchCommand;
using EffiAP.Application.Commands.BranchCommands;
using EffiAP.Domain.Entities;
using EffiAP.Infrastructure.IRepositories;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EffiAP.Application.Tests.Commands.BranchCommands
{
    public class CreateBranchCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository> _mockGenericRepo;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly CreateBranchCommandHandler _handler;

        public CreateBranchCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockGenericRepo = new Mock<IGenericRepository>();
            _mockConfiguration = new Mock<IConfiguration>();

            _mockUnitOfWork.Setup(u => u.Repository).Returns(_mockGenericRepo.Object);
            _handler = new CreateBranchCommandHandler(_mockUnitOfWork.Object, _mockConfiguration.Object);
        }

        [Fact]
        public async Task Handle_ShouldCreateBranch_AndReturnBranchId()
        {
            // Arrange
            var request = new CreateBranchCommand
            {
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "Test Branch",
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "test@branch.com"
            };

            _mockGenericRepo
                .Setup(repo => repo.AddAsync(It.IsAny<Branch>()))
                .Returns(Task.CompletedTask);
            _mockUnitOfWork
                .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotEqual(Guid.Empty, result); // Kiểm tra BranchID không rỗng
            _mockGenericRepo.Verify(repo => repo.AddAsync(It.Is<Branch>(b =>
                b.OwnerId == request.OwnerId &&
                b.BranchName == request.BranchName &&
                b.Address == request.Address &&
                b.Phone == request.Phone &&
                b.Email == request.Email &&
                b.Rooms != null && b.Rooms.Count == 0)), Times.Once());
            _mockUnitOfWork.Verify(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task Handle_ShouldThrowArgumentException_WhenBranchNameIsEmpty()
        {
            // Arrange
            var request = new CreateBranchCommand
            {
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "", // Tên chi nhánh rỗng
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "test@branch.com"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(request, CancellationToken.None));
            _mockGenericRepo.Verify(repo => repo.AddAsync(It.IsAny<Branch>()), Times.Never());
            _mockUnitOfWork.Verify(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldThrowArgumentException_WhenEmailIsInvalid()
        {
            // Arrange
            var request = new CreateBranchCommand
            {
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "Test Branch",
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "invalid-email" // Email không hợp lệ
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(request, CancellationToken.None));
            _mockGenericRepo.Verify(repo => repo.AddAsync(It.IsAny<Branch>()), Times.Never());
            _mockUnitOfWork.Verify(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldThrowException_WhenSaveEntitiesFails()
        {
            // Arrange
            var request = new CreateBranchCommand
            {
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "Test Branch",
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "test@branch.com"
            };

            _mockGenericRepo
                .Setup(repo => repo.AddAsync(It.IsAny<Branch>()))
                .Returns(Task.CompletedTask);
            _mockUnitOfWork
                .Setup(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handler.Handle(request, CancellationToken.None));
            _mockGenericRepo.Verify(repo => repo.AddAsync(It.IsAny<Branch>()), Times.Once());
            _mockUnitOfWork.Verify(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task Handle_ShouldThrowOperationCanceled_WhenCancellationRequested()
        {
            // Arrange
            var request = new CreateBranchCommand
            {
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "Test Branch",
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "test@branch.com"
            };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => _handler.Handle(request, cts.Token));
            _mockGenericRepo.Verify(repo => repo.AddAsync(It.IsAny<Branch>()), Times.Never());
            _mockUnitOfWork.Verify(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldCreateBranch_WithMaxLengthBranchName()
        {
            // Arrange
            var maxLengthName = new string('A', 100); // Giả định độ dài tối đa là 100
            var request = new CreateBranchCommand
            {
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = maxLengthName,
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "test@branch.com"
            };

            _mockGenericRepo
                .Setup(repo => repo.AddAsync(It.IsAny<Branch>()))
                .Returns(Task.CompletedTask);
            _mockUnitOfWork
                .Setup(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotEqual(Guid.Empty, result);
            _mockGenericRepo.Verify(repo => repo.AddAsync(It.Is<Branch>(b =>
                b.BranchName == maxLengthName)), Times.Once());
            _mockUnitOfWork.Verify(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task Handle_ShouldThrowArgumentException_WhenOwnerIdIsEmpty()
        {
            // Arrange
            var request = new CreateBranchCommand
            {
                OwnerId = "",
                BranchName = "Test Branch",
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "test@branch.com"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(request, CancellationToken.None));
            _mockGenericRepo.Verify(repo => repo.AddAsync(It.IsAny<Branch>()), Times.Never());
            _mockUnitOfWork.Verify(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldThrowException_WhenBranchNameExists()
        {
            // Arrange
            var request = new CreateBranchCommand
            {
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "Existing Branch",
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "test@branch.com"
            };

            _mockGenericRepo
                .Setup(repo => repo.AddAsync(It.IsAny<Branch>()))
                .ThrowsAsync(new Exception("Duplicate branch name"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handler.Handle(request, CancellationToken.None));
            _mockUnitOfWork.Verify(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never());
        }
    }
}


//using EffiAP.Application.Commands.BranchCommand;
//using EffiAP.Application.Commands.BranchCommands;
//using EffiAP.Domain.Entities;
//using EffiAP.Infrastructure.IRepositories;
//using Microsoft.Extensions.Configuration;
//using Moq;

//namespace EffiRent.Application.Tests
//{
//    public class CreateBranchCommandHandlerTests
//    {
//        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
//        private readonly Mock<IGenericRepository> _mockGenericRepo;
//        private readonly Mock<IConfiguration> _mockConfig;
//        private readonly CreateBranchCommandHandler _handler;

//        public CreateBranchCommandHandlerTests()
//        {
//            _mockUnitOfWork = new Mock<IUnitOfWork>();
//            _mockGenericRepo = new Mock<IGenericRepository>();
//            _mockConfig = new Mock<IConfiguration>();

//            // Thiết lập repo chung từ UnitOfWork
//            _mockUnitOfWork.Setup(u => u.Repository).Returns(_mockGenericRepo.Object);

//            _handler = new CreateBranchCommandHandler(_mockUnitOfWork.Object, _mockConfig.Object);
//        }

//        [Fact]
//        public async Task Handle_ShouldCreateBranch_AndReturnGuid()
//        {
//            // Arrange
//            var request = new CreateBranchCommand
//            {
//                OwnerId = Guid.NewGuid().ToString(),
//                BranchName = "Chi nhánh Hà Nội",
//                Address = "123 Lê Duẩn",
//                Phone = "0123456789",
//                Email = "hanoi@effiap.vn"
//            };

//            // Setup mock repo
//            _mockGenericRepo
//                .Setup(repo => repo.AddAsync(It.IsAny<Branch>()))
//                .Returns(Task.CompletedTask);

//            _mockUnitOfWork
//                .Setup(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
//                .ReturnsAsync(true);

//            // Act
//            var result = await _handler.Handle(request, CancellationToken.None);

//            // Assert
//            Assert.IsType<Guid>(result);
//            Assert.NotEqual(Guid.Empty, result);

//            _mockGenericRepo.Verify(repo => repo.AddAsync(It.Is<Branch>(b =>
//                b.BranchName == request.BranchName &&
//                b.Address == request.Address &&
//                b.Phone == request.Phone &&
//                b.Email == request.Email &&
//                b.OwnerId == request.OwnerId
//            )), Times.Once);

//            _mockUnitOfWork.Verify(uow => uow.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
//        }
//    }

//}