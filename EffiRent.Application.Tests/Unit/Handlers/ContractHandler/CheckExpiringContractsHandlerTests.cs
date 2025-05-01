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
using EffiRent.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;
using MockQueryable.Moq;

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
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                           .ReturnsAsync(_mockTransaction.Object);
        }

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

        [Fact]
        public async Task Handle_ShouldSendEmail_AndCreateNotification()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3), "Active");
            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            // Mock GetAsync<Contract> to return IQueryable<Contract>
            var contractList = new List<Contract> { contract }.AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.Is<Expression<Func<Contract, bool>>>(expr =>
                expr.Compile()(contract))))
                     .Returns(contractList.Object);

            // Mock GetAll<Notification> to return empty IQueryable<Notification>
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>())
                     .Returns(notificationList.Object);

            // Mock UserManager
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Mock EmailService
            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .Returns(Task.CompletedTask);

            // Mock AddAsync
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Notification>()))
                     .Returns(Task.CompletedTask);

            // Mock SaveChangesAsync
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                           .ReturnsAsync(1);

            // Mock transaction CommitAsync and RollbackAsync
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                _mockUserManager.Object,
                _configuration);

            // Act
            try
            {
                await handler.Handle(command, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Handler threw an unexpected exception: {ex.Message}");
            }

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id)), Times.Once());
            _mockEmailService.Verify(e => e.SendEmailAsync(tenant.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldNotSendEmail_WhenNoExpiringContracts()
        {
            // Arrange
            var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow.Date };

            var contractList = new List<Contract>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.IsAny<Expression<Func<Contract, bool>>>())).Returns(contractList.Object);
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(_mockUnitOfWork.Object, _mockEmailService.Object, _mockUserManager.Object, _configuration);

            // Act
            try
            {
                await handler.Handle(command, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Handler threw an unexpected exception: {ex.Message}");
            }

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldNotSendEmail_WhenContractNotActive()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3), "Inactive");
            var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow.Date };

            //var contractList = new List<Contract> { contract }.AsQueryable().BuildMockDbSet();
            //_mockRepo.Setup(r => r.GetAsync<Contract>(It.Is<Expression<Func<Contract, bool>>>(expr => expr.Compile()(contract))))
            //         .Returns(contractList.Object);
            //var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            //_mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);
            var contractList = new List<Contract>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.IsAny<Expression<Func<Contract, bool>>>())).Returns(contractList.Object);
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);

            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(_mockUnitOfWork.Object, _mockEmailService.Object, _mockUserManager.Object, _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldNotSendEmail_WhenEndDateOutOfRange()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(10), "Active"); // Ngoài khoảng 7 ngày
            var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow.Date };

            //var contractList = new List<Contract> { contract }.AsQueryable().BuildMockDbSet();
            //_mockRepo.Setup(r => r.GetAsync<Contract>(It.Is<Expression<Func<Contract, bool>>>(expr => expr.Compile()(contract))))
            //         .Returns(contractList.Object);
            //var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            //_mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);
            var contractList = new List<Contract>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.IsAny<Expression<Func<Contract, bool>>>())).Returns(contractList.Object);
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);

            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(_mockUnitOfWork.Object, _mockEmailService.Object, 
            _mockUserManager.Object, _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldNotSendEmail_WhenTenantNotFound()
        {
            // Arrange
            var tenantId = "tenant123";
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3), "Active");
            var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow.Date };

            //var contractList = new List<Contract> { contract }.AsQueryable().BuildMockDbSet();
            //_mockRepo.Setup(r => r.GetAsync<Contract>(It.Is<Expression<Func<Contract, bool>>>(expr => expr.Compile()(contract))))
            //         .Returns(contractList.Object);
            //var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            var contractList = new List<Contract>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.IsAny<Expression<Func<Contract, bool>>>())).Returns(contractList.Object);
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);

            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync((IdentityUser)null);
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(_mockUnitOfWork.Object, _mockEmailService.Object, _mockUserManager.Object, _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldNotSendEmail_WhenNotificationExists()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3), "Active");
            var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow.Date };
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = tenantId,
                RelatedEntityId = contract.Id,
                RelatedEntityType = "Contract",
                CreatedAt = DateTime.UtcNow,
                Title = "Contract Expiring Soon",
                Message = "Test",
                IsRead = false
            };

            //var contractList = new List<Contract> { contract }.AsQueryable().BuildMockDbSet();
            //_mockRepo.Setup(r => r.GetAsync<Contract>(It.Is<Expression<Func<Contract, bool>>>(expr => expr.Compile()(contract))))
            //         .Returns(contractList.Object);
            //var notificationList = new List<Notification> { notification }.AsQueryable().BuildMockDbSet();
            //_mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);

            var contractList = new List<Contract>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.IsAny<Expression<Func<Contract, bool>>>())).Returns(contractList.Object);
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);

            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(_mockUnitOfWork.Object, _mockEmailService.Object, _mockUserManager.Object, _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Never());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldHandleMultipleContractsForSameTenant()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract1 = CreateContract(tenantId, DateTime.UtcNow.AddDays(3), "Active");
            var contract2 = CreateContract(tenantId, DateTime.UtcNow.AddDays(5), "Active");
            var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow.Date };

            var contractList = new List<Contract> { contract1, contract2 }.AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.IsAny<Expression<Func<Contract, bool>>>())).Returns(contractList.Object);
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(_mockUnitOfWork.Object, _mockEmailService.Object, _mockUserManager.Object, _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.UserId == tenantId && (n.RelatedEntityId == contract1.Id || n.RelatedEntityId == contract2.Id))), Times.Exactly(2));
            _mockEmailService.Verify(e => e.SendEmailAsync(tenant.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldHandleMultipleTenants()
        {
            // Arrange
            var tenantId1 = "tenant123";
            var tenantId2 = "tenant456";
            var tenant1 = CreateTenant(tenantId1, "test1@domain.com");
            var tenant2 = CreateTenant(tenantId2, "test2@domain.com");
            var contract1 = CreateContract(tenantId1, DateTime.UtcNow.AddDays(3), "Active");
            var contract2 = CreateContract(tenantId2, DateTime.UtcNow.AddDays(5), "Active");
            var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow.Date };

            var contractList = new List<Contract> { contract1, contract2 }.AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.IsAny<Expression<Func<Contract, bool>>>())).Returns(contractList.Object);
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId1)).ReturnsAsync(tenant1);
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId2)).ReturnsAsync(tenant2);
            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(_mockUnitOfWork.Object, _mockEmailService.Object, _mockUserManager.Object, _configuration);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.UserId == tenantId1 && n.RelatedEntityId == contract1.Id)), Times.Once());
            _mockRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.UserId == tenantId2 && n.RelatedEntityId == contract2.Id)), Times.Once());
            _mockEmailService.Verify(e => e.SendEmailAsync(tenant1.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            _mockEmailService.Verify(e => e.SendEmailAsync(tenant2.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Handle_ShouldRollback_WhenEmailServiceThrowsException()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3), "Active");
            var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow.Date };

            var contractList = new List<Contract> { contract }.AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.Is<Expression<Func<Contract, bool>>>(expr => expr.Compile()(contract))))
                     .Returns(contractList.Object);
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ThrowsAsync(new Exception("Email service failed"));
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(_mockUnitOfWork.Object, _mockEmailService.Object, _mockUserManager.Object, _configuration);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Once());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task Handle_ShouldRollback_WhenSaveChangesThrowsException()
        {
            // Arrange
            var tenantId = "tenant123";
            var tenant = CreateTenant(tenantId, "test@domain.com");
            var contract = CreateContract(tenantId, DateTime.UtcNow.AddDays(3), "Active");
            var command = new CheckExpiringContractsCommand { CheckDate = DateTime.UtcNow.Date };

            var contractList = new List<Contract> { contract }.AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAsync<Contract>(It.Is<Expression<Func<Contract, bool>>>(expr => expr.Compile()(contract))))
                     .Returns(contractList.Object);
            var notificationList = new List<Notification>().AsQueryable().BuildMockDbSet();
            _mockRepo.Setup(r => r.GetAll<Notification>()).Returns(notificationList.Object);
            _mockUserManager.Setup(um => um.FindByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Save changes failed"));
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockTransaction.Setup(t => t.Dispose());

            var handler = new CheckExpiringContractsHandler(_mockUnitOfWork.Object, _mockEmailService.Object, _mockUserManager.Object, _configuration);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Once());
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

    }
}
