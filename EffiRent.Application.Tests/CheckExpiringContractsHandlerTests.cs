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
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EffiAP.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace EffiRent.Application.Tests.Handlers.ContractHandler
{
    public class CheckExpiringContractsHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository> _mockRepo;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<UserManager<IdentityUser>> _mockUserManager;
        private readonly Mock<IDbContextTransaction> _mockTransaction;
        private readonly IConfiguration _configuration;

        public CheckExpiringContractsHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository>();
            _mockEmailService = new Mock<IEmailService>();
            var store = new Mock<IUserStore<IdentityUser>>();
            _mockUserManager = new Mock<UserManager<IdentityUser>>(store.Object, null, null, null, null, null, null, null, null);
            _mockTransaction = new Mock<IDbContextTransaction>();
            _configuration = new ConfigurationBuilder().Build();

            _mockUnitOfWork.Setup(x => x.Repository).Returns(_mockRepo.Object);
            _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(_mockTransaction.Object);
        }

        // Builder để tạo dữ liệu kiểm thử
        private Contract CreateContract(string tenantId, DateTime endDate, string status = "Active")
        {
            return new Contract
            {
                Id = Guid.NewGuid(),
                Status = status,
                EndDate = endDate,
                TenantId = tenantId,
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied
                    }
                }
            };
        }

        private IdentityUser CreateTenant(string tenantId, string email)
        {
            return new IdentityUser { Id = tenantId, Email = email };
        }

        //[Fact]
        //public async Task Handle_ShouldSendEmail_AndCreateNotification()
        //{
        //    // Arrange
        //    var tenantId = "tenant123";
        //    var tenant = CreateTenant(tenantId, "test@domain.com");
        //    var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3));
        //    var command = new CheckExpiringContractsCommand
        //    {
        //        CheckDate = DateTime.UtcNow.Date
        //    };

        //    _mockRepo.Setup(r => r.Get<Contract>(It.IsAny<Expression<Func<Contract, bool>>>()))
        //             .Returns(new List<Contract> { contract }.AsQueryable());
        //    _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
        //    _mockRepo.Setup(r => r.Get<Notification>(It.IsAny<Expression<Func<Notification, bool>>>()))
        //             .Returns(new List<Notification>().AsQueryable());
        //    _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
        //                     .Returns(Task.CompletedTask);
        //    _mockRepo.Setup(r => r.AddAsync(It.IsAny<Notification>()))
        //             .Returns(Task.CompletedTask);
        //    _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
        //                   .ReturnsAsync(1);
        //    _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
        //                    .Returns(Task.CompletedTask);

        //    var handler = new CheckExpiringContractsHandler(
        //        _mockUnitOfWork.Object,
        //        _mockEmailService.Object,
        //        _mockUserManager.Object,
        //        _configuration);

        //    // Act
        //    try
        //    {
        //        await handler.Handle(command, CancellationToken.None);
        //    }
        //    catch (Exception ex)
        //    {
        //        Assert.Fail($"Handler threw an unexpected exception: {ex.Message}");
        //    }

        //    // Assert
        //    _mockRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id)), Times.Once());
        //    _mockEmailService.Verify(e => e.SendEmailAsync(tenant.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        //    _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        //    _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
        //}

        [Fact]
        public async Task Handle_ShouldNotSendEmail_WhenNoExpiringContracts()
        {
            // Arrange
            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            _mockRepo.Setup(r => r.Get<Contract>(It.IsAny<Expression<Func<Contract, bool>>>()))
                     .Returns(new List<Contract>().AsQueryable());

            var handler = new CheckExpiringContractsHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockUserManager.Object,
                _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldNotSendEmail_WhenNotificationAlreadyExists()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3));
            var existingNotification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = tenantId,
                RelatedEntityId = contract.Id,
                RelatedEntityType = "Contract",
                CreatedAt = DateTime.UtcNow
            };
            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            _mockRepo.Setup(r => r.Get<Contract>(It.IsAny<Expression<Func<Contract, bool>>>()))
                     .Returns(new List<Contract> { contract }.AsQueryable());
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockRepo.Setup(r => r.Get<Notification>(It.IsAny<Expression<Func<Notification, bool>>>()))
                     .Returns(new List<Notification> { existingNotification }.AsQueryable());
            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                             .Returns(Task.CompletedTask);
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Notification>()))
                     .Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                           .ReturnsAsync(1);

            var handler = new CheckExpiringContractsHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockUserManager.Object,
                _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldRollbackTransaction_WhenEmailFails()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3));
            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            _mockRepo.Setup(r => r.Get<Contract>(It.IsAny<Expression<Func<Contract, bool>>>()))
                     .Returns(new List<Contract> { contract }.AsQueryable());
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockRepo.Setup(r => r.Get<Notification>(It.IsAny<Expression<Func<Notification, bool>>>()))
                     .Returns(new List<Notification>().AsQueryable());
            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                             .ThrowsAsync(new Exception("Email service failed"));
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Notification>()))
                     .Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                           .ReturnsAsync(1);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>()))
                            .Returns(Task.CompletedTask);

            var handler = new CheckExpiringContractsHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockUserManager.Object,
                _configuration);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Email service failed", exception.Message);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Once());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldThrowException_WhenTenantNotFound()
        {
            // Arrange
            var tenantId = "tenant123";
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3));
            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            _mockRepo.Setup(r => r.Get<Contract>(It.IsAny<Expression<Func<Contract, bool>>>()))
                     .Returns(new List<Contract> { contract }.AsQueryable());
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync((IdentityUser)null);
            _mockRepo.Setup(r => r.Get<Notification>(It.IsAny<Expression<Func<Notification, bool>>>()))
                     .Returns(new List<Notification>().AsQueryable());

            var handler = new CheckExpiringContractsHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockUserManager.Object,
                _configuration);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Tenant not found", exception.Message);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldThrowOperationCanceled_WhenCancellationRequested()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3));
            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockRepo.Setup(r => r.Get<Contract>(It.IsAny<Expression<Func<Contract, bool>>>()))
                     .Returns(new List<Contract> { contract }.AsQueryable());
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId))
                            .ReturnsAsync(tenant);
            _mockRepo.Setup(r => r.Get<Notification>(It.IsAny<Expression<Func<Notification, bool>>>()))
                     .Returns(new List<Notification>().AsQueryable());
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync())
                           .ReturnsAsync(_mockTransaction.Object);
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Notification>()))
                     .ThrowsAsync(new OperationCanceledException("Operation canceled"));
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new OperationCanceledException("Operation canceled"));
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            var handler = new CheckExpiringContractsHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockUserManager.Object,
                _configuration);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, cts.Token));
            Assert.IsType<OperationCanceledException>(exception.InnerException);
            Assert.Contains("Operation canceled", exception.Message);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Once());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task Handle_ShouldSkipNonActiveContracts()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3), status: "Terminated");
            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            // Thiết lập mock để trả về danh sách rỗng nếu hợp đồng không phải Active
            _mockRepo.Setup(r => r.Get<Contract>(It.Is<Expression<Func<Contract, bool>>>(expr =>
                expr.Compile()(contract) == false)))
                     .Returns(new List<Contract>().AsQueryable());
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockRepo.Setup(r => r.Get<Notification>(It.IsAny<Expression<Func<Notification, bool>>>()))
                     .Returns(new List<Notification>().AsQueryable());
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync())
                           .ReturnsAsync(_mockTransaction.Object);

            var handler = new CheckExpiringContractsHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockUserManager.Object,
                _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldSkipContractsOutsideThreshold()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(10)); // Ngoài ngưỡng 7 ngày
            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            // Thiết lập mock để trả về danh sách rỗng nếu hợp đồng ngoài ngưỡng
            _mockRepo.Setup(r => r.Get<Contract>(It.Is<Expression<Func<Contract, bool>>>(expr =>
                expr.Compile()(contract) == false)))
                     .Returns(new List<Contract>().AsQueryable());
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockRepo.Setup(r => r.Get<Notification>(It.IsAny<Expression<Func<Notification, bool>>>()))
                     .Returns(new List<Notification>().AsQueryable());
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync())
                           .ReturnsAsync(_mockTransaction.Object);

            var handler = new CheckExpiringContractsHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockUserManager.Object,
                _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>()), Times.Never());
        }
    }
}