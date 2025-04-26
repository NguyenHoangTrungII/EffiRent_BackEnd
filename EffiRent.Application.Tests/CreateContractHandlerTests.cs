using EffiRent.Application.Commands.ContractCommand;
using EffiRent.Application.Handlers.ContractHandler;
using EffiRent.Application.Services.Email;
using EffiRent.Domain.Entities;
using EffiAP.Infrastructure.IRepositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EffiAP.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace EffiRent.Application.Tests.Handlers.ContractHandler
{
    public class CreateContractHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository> _mockGenericRepo;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<UserManager<IdentityUser>> _mockUserManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IDbContextTransaction> _mockTransaction;
        private readonly CreateContractHandler _handler;

        public CreateContractHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockGenericRepo = new Mock<IGenericRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _mockUserManager = new Mock<UserManager<IdentityUser>>(
                Mock.Of<IUserStore<IdentityUser>>(), null, null, null, null, null, null, null, null);
            _mockConfiguration = new Mock<IConfiguration>();
            _mockTransaction = new Mock<IDbContextTransaction>();

            _mockUnitOfWork.Setup(u => u.Repository).Returns(_mockGenericRepo.Object);
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_mockTransaction.Object);

            _handler = new CreateContractHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockUserManager.Object,
                _mockConfiguration.Object);
        }

        [Fact]
        public async Task Handle_NenTaoHopDongThanhCong_VaTraVeContractId()
        {
            // Arrange
            var tenantId = Guid.NewGuid().ToString();
            var roomId = Guid.NewGuid();
            var tenant = new IdentityUser { Id = tenantId, Email = "tenant@example.com" };
            var room = new Room
            {
                Id = roomId,
                Name = "Room 101",
                Status = Room.RoomStatus.Available
            };
            var request = new CreateContractCommand
            {
                TenantId = tenantId,
                RoomId = roomId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6),
                RentAmount = 1000,
                DepositAmount = 500,
                PaymentMethod = "Bank Transfer",
                Terms = "Standard terms"
            };

            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockGenericRepo.Setup(r => r.GetByIdAsync<Room>(roomId)).ReturnsAsync(room);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockEmailService.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotEqual(Guid.Empty, result);
            _mockGenericRepo.Verify(r => r.UpdateAsync<Room>(It.Is<Room>(rm => rm.Status == Room.RoomStatus.Occupied)), Times.Once());
            _mockGenericRepo.Verify(r => r.AddAsync<Contract>(It.IsAny<Contract>()), Times.Once());
            _mockGenericRepo.Verify(r => r.AddAsync<Payment>(It.IsAny<Payment>()), Times.Once());
            _mockGenericRepo.Verify(r => r.AddAsync<Notification>(It.IsAny<Notification>()), Times.Once());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockEmailService.Verify(e => e.SendEmailAsync(tenant.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public async Task Handle_NenNemException_KhiTenantKhongTonTai()
        {
            // Arrange
            var request = new CreateContractCommand
            {
                TenantId = Guid.NewGuid().ToString(),
                RoomId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6),
                RentAmount = 1000,
                DepositAmount = 500,
                PaymentMethod = "Bank Transfer",
                Terms = "Standard terms"
            };

            _mockUserManager.Setup(um => um.FindByIdAsync(request.TenantId)).ReturnsAsync((IdentityUser)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(request, CancellationToken.None));
            Assert.Equal("Tenant not found.", exception.Message);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_NenNemException_KhiRoomKhongTonTai()
        {
            // Arrange
            var tenantId = Guid.NewGuid().ToString();
            var tenant = new IdentityUser { Id = tenantId, Email = "tenant@example.com" };
            var request = new CreateContractCommand
            {
                TenantId = tenantId,
                RoomId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6),
                RentAmount = 1000,
                DepositAmount = 500,
                PaymentMethod = "Bank Transfer",
                Terms = "Standard terms"
            };

            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockGenericRepo.Setup(r => r.GetByIdAsync<Room>(request.RoomId)).ReturnsAsync((Room)null);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(request, CancellationToken.None));
            Assert.Equal("Failed to create contract: Room not found.", exception.Message);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
        }


        [Fact]
        public async Task Handle_NenNemException_KhiRoomKhongKhaDung()
        {
            // Arrange
            var tenantId = Guid.NewGuid().ToString();
            var roomId = Guid.NewGuid();
            var tenant = new IdentityUser { Id = tenantId, Email = "tenant@example.com" };
            var room = new Room
            {
                Id = roomId,
                Name = "Room 101",
                Status = Room.RoomStatus.Occupied // Phòng đã được thuê
            };
            var request = new CreateContractCommand
            {
                TenantId = tenantId,
                RoomId = roomId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6),
                RentAmount = 1000,
                DepositAmount = 500,
                PaymentMethod = "Bank Transfer",
                Terms = "Standard terms"
            };

            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockGenericRepo.Setup(r => r.GetByIdAsync<Room>(roomId)).ReturnsAsync(room);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(request, CancellationToken.None));
            Assert.Equal("Failed to create contract: Room is not available.", exception.Message);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        //[Fact]
        //public async Task Handle_NenRollbackTransaction_KhiEmailGuiThatBai()
        //{
        //    // Arrange
        //    var tenantId = Guid.NewGuid().ToString();
        //    var roomId = Guid.NewGuid();
        //    var tenant = new IdentityUser { Id = tenantId, Email = "tenant@example.com" };
        //    var room = new Room
        //    {
        //        Id = roomId,
        //        Name = "Room 101",
        //        Status = Room.RoomStatus.Available
        //    };
        //    var request = new CreateContractCommand
        //    {
        //        TenantId = tenantId,
        //        RoomId = roomId,
        //        StartDate = DateTime.UtcNow,
        //        EndDate = DateTime.UtcNow.AddMonths(6),
        //        RentAmount = 1000,
        //        DepositAmount = 500,
        //        PaymentMethod = "Bank Transfer",
        //        Terms = "Standard terms"
        //    };

        //    _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
        //    _mockGenericRepo.Setup(r => r.GetByIdAsync<Room>(roomId)).ReturnsAsync(room);
        //    _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        //    _mockEmailService.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
        //        .ThrowsAsync(new Exception("Email service failed"));
        //    _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        //    // Act & Assert
        //    var exception = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(request, CancellationToken.None));
        //    Assert.Contains("Failed to create contract", exception.Message);
        //    _mockGenericRepo.Verify(r => r.AddAsync<Contract>(It.IsAny<Contract>()), Times.Once());
        //    _mockGenericRepo.Verify(r => r.UpdateAsync<Room>(It.Is<Room>(rm => rm.Status == Room.RoomStatus.Occupied)), Times.Once());
        //    _mockGenericRepo.Verify(r => r.AddAsync<Payment>(It.IsAny<Payment>()), Times.Once());
        //    _mockGenericRepo.Verify(r => r.AddAsync<Notification>(It.IsAny<Notification>()), Times.Once());
        //    _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        //    _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
        //    _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
        //}
    }
}