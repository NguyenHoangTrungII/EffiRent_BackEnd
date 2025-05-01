using EffiRent.Domain.Entities;
using EffiAP.Infrastructure.EntityModels;
using EffiAP.Infrastructure.IRepositories;
using EffiAP.Infrastructure.Repositories;
using EffiRent.Application.Commands.ContractCommand;
using EffiRent.Application.Handlers.ContractHandler;
using EffiRent.Application.Services.Email;
using EffiRent.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EffiRent.Application.Tests.Integration.Handlers.ContractHandler
{
    public class CheckExpiringContractsHandlerIntegrationTests : IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly EffiRentContext _dbContext;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;
        private readonly CheckExpiringContractsHandler _handler;
        private readonly SqliteConnection _connection;
        private readonly TestDataBuilder _dataBuilder;

        public CheckExpiringContractsHandlerIntegrationTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddDbContext<EffiRentContext>(options =>
                options.UseSqlite(_connection));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<EffiRentContext>()
                .AddDefaultTokenProviders();

            var serviceProvider = services.BuildServiceProvider();
            _scope = serviceProvider.CreateScope();

            _dbContext = _scope.ServiceProvider.GetRequiredService<EffiRentContext>();
            _dbContext.Database.EnsureCreated();

            _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            _mockEmailService = new Mock<IEmailService>();
            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .Returns(Task.CompletedTask);

            _configuration = new ConfigurationBuilder().Build();

            _unitOfWork = _scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            _handler = new CheckExpiringContractsHandler(
                _unitOfWork,
                _mockEmailService.Object,
                _userManager,
                _configuration);

            _dataBuilder = new TestDataBuilder(_dbContext, _userManager);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
            _scope.Dispose();
        }

        // Builder để tạo dữ liệu kiểm thử
        private class TestDataBuilder
        {
            private readonly EffiRentContext _dbContext;
            private readonly UserManager<IdentityUser> _userManager;

            public TestDataBuilder(EffiRentContext dbContext, UserManager<IdentityUser> userManager)
            {
                _dbContext = dbContext;
                _userManager = userManager;
            }

            public async Task<IdentityUser> CreateTenantAsync(string email = null)
            {
                var tenantId = Guid.NewGuid().ToString();
                var userName = email ?? $"tenant_{Guid.NewGuid()}@domain.com";
                var tenant = new IdentityUser
                {
                    Id = tenantId,
                    UserName = userName,
                    Email = userName
                };
                await _userManager.CreateAsync(tenant);
                return tenant;
            }

            public async Task<IdentityUser> CreateOwnerAsync()
            {
                var ownerId = Guid.NewGuid().ToString();
                var userName = $"owner_{Guid.NewGuid()}@domain.com";
                var owner = new IdentityUser
                {
                    Id = ownerId,
                    UserName = userName,
                    Email = userName
                };
                await _userManager.CreateAsync(owner);
                return owner;
            }

            public async Task<Branch> CreateBranchAsync(string ownerId)
            {
                var branch = new Branch
                {
                    BranchID = Guid.NewGuid(),
                    BranchName = "Chi nhánh 1",
                    Address = "123 Đường Nguyễn Thị Minh Khai, Quận 1, Hồ Chí Minh",
                    Phone = "0901234567",
                    Email = "branch1@example.com",
                    OwnerId = ownerId
                };
                await _dbContext.Branch.AddAsync(branch);
                await _dbContext.SaveChangesAsync();
                return branch;
            }

            public async Task<(Room, TenantRoom, Contract)> CreateContractRelatedDataAsync(
                string tenantId, Branch branch, Contract contract)
            {
                var room = contract.TenantRoom.Room;
                room.BranchID = branch.BranchID;
                room.Notes = "Room notes";
                await _dbContext.Room.AddAsync(room);

                var tenantRoom = contract.TenantRoom;
                tenantRoom.TenantRoomID = Guid.NewGuid();
                tenantRoom.TenantID = tenantId;
                tenantRoom.RoomID = room.Id;
                tenantRoom.StartDate = DateTime.UtcNow.AddMonths(-6);
                await _dbContext.TenantRoom.AddAsync(tenantRoom);

                contract.TenantRoomId = tenantRoom.TenantRoomID;
                await _dbContext.Contracts.AddAsync(contract);

                await _dbContext.SaveChangesAsync();
                return (room, tenantRoom, contract);
            }
        }

        [Fact]
        public async Task Handle_NenGuiEmailVaTaoThongBao_ChoHopDongSapHetHan()
        {
            var tenant = await _dataBuilder.CreateTenantAsync("test@domain.com");
            var owner = await _dataBuilder.CreateOwnerAsync();
            var branch = await _dataBuilder.CreateBranchAsync(owner.Id);
            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Active",
                EndDate = DateTime.UtcNow.AddDays(3),
                TenantId = tenant.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1000,
                DepositAmount = 2000,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };

            await _dataBuilder.CreateContractRelatedDataAsync(tenant.Id, branch, contract);

            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            await _handler.Handle(command, CancellationToken.None);

            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.UserId == tenant.Id && n.RelatedEntityId == contract.Id);

            Assert.NotNull(notification);
            Assert.Equal(tenant.Id, notification.UserId);
            Assert.Contains("Contract Expiring Soon", notification.Title);
            Assert.False(notification.IsRead);

            _mockEmailService.Verify(es => es.SendEmailAsync(
                tenant.Email,
                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
                Times.Once());
        }

        [Fact]
        public async Task Handle_WhenEmailFails_ShouldRollbackTransaction()
        {
            var tenant = await _dataBuilder.CreateTenantAsync("test@domain.com");
            var owner = await _dataBuilder.CreateOwnerAsync();
            var branch = await _dataBuilder.CreateBranchAsync(owner.Id);
            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Active",
                EndDate = DateTime.UtcNow.AddDays(3),
                TenantId = tenant.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1000,
                DepositAmount = 2000,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };

            await _dataBuilder.CreateContractRelatedDataAsync(tenant.Id, branch, contract);

            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ThrowsAsync(new Exception("Email failed"));

            await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));

            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.UserId == tenant.Id && n.RelatedEntityId == contract.Id);

            Assert.Null(notification);
        }

        [Fact]
        public async Task Handle_NoContractsWithinSevenDays_ShouldNotCreateNotification()
        {
            var tenant = await _dataBuilder.CreateTenantAsync("test@domain.com");
            var owner = await _dataBuilder.CreateOwnerAsync();
            var branch = await _dataBuilder.CreateBranchAsync(owner.Id);
            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Active",
                EndDate = DateTime.UtcNow.AddDays(10),
                TenantId = tenant.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1000,
                DepositAmount = 2000,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };

            await _dataBuilder.CreateContractRelatedDataAsync(tenant.Id, branch, contract);

            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            await _handler.Handle(command, CancellationToken.None);

            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.UserId == tenant.Id && n.RelatedEntityId == contract.Id);

            Assert.Null(notification);
            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                                    Times.Never());
        }

        [Fact]
        public async Task Handle_ContractExpiringToday_ShouldCreateNotificationAndSendEmail()
        {
            var tenant = await _dataBuilder.CreateTenantAsync("test@domain.com");
            var owner = await _dataBuilder.CreateOwnerAsync();
            var branch = await _dataBuilder.CreateBranchAsync(owner.Id);
            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Active",
                EndDate = DateTime.UtcNow.Date,
                TenantId = tenant.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1000,
                DepositAmount = 2000,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };

            await _dataBuilder.CreateContractRelatedDataAsync(tenant.Id, branch, contract);

            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            await _handler.Handle(command, CancellationToken.None);

            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.UserId == tenant.Id && n.RelatedEntityId == contract.Id);

            Assert.NotNull(notification);
            Assert.Equal(tenant.Id, notification.UserId);
            Assert.Contains("Contract Expiring Soon", notification.Title);
            Assert.False(notification.IsRead);

            _mockEmailService.Verify(es => es.SendEmailAsync(
                tenant.Email,
                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
                Times.Once());
        }

        [Fact]
        public async Task Handle_ContractExpiringExactlySevenDays_ShouldCreateNotificationAndSendEmail()
        {
            var tenant = await _dataBuilder.CreateTenantAsync("test@domain.com");
            var owner = await _dataBuilder.CreateOwnerAsync();
            var branch = await _dataBuilder.CreateBranchAsync(owner.Id);
            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Active",
                EndDate = DateTime.UtcNow.AddDays(7).Date,
                TenantId = tenant.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1000,
                DepositAmount = 2000,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };

            await _dataBuilder.CreateContractRelatedDataAsync(tenant.Id, branch, contract);

            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            await _handler.Handle(command, CancellationToken.None);

            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.UserId == tenant.Id && n.RelatedEntityId == contract.Id);

            Assert.NotNull(notification);
            Assert.Equal(tenant.Id, notification.UserId);
            Assert.Contains("Contract Expiring Soon", notification.Title);
            Assert.False(notification.IsRead);

            _mockEmailService.Verify(es => es.SendEmailAsync(
                tenant.Email,
                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
                Times.Once());
        }

        [Fact]
        public async Task Handle_MultipleContractsForSameTenant_ShouldCreateOneNotificationPerContract()
        {
            var tenant = await _dataBuilder.CreateTenantAsync("test@domain.com");
            var owner = await _dataBuilder.CreateOwnerAsync();
            var branch = await _dataBuilder.CreateBranchAsync(owner.Id);

            var contract1 = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Active",
                EndDate = DateTime.UtcNow.AddDays(3),
                TenantId = tenant.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1000,
                DepositAmount = 2000,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };
            var contract2 = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Active",
                EndDate = DateTime.UtcNow.AddDays(5),
                TenantId = tenant.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1200,
                DepositAmount = 2400,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A102",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };

            await _dataBuilder.CreateContractRelatedDataAsync(tenant.Id, branch, contract1);
            await _dataBuilder.CreateContractRelatedDataAsync(tenant.Id, branch, contract2);

            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            await _handler.Handle(command, CancellationToken.None);

            var notifications = await _dbContext.Notifications
                .Where(n => n.UserId == tenant.Id)
                .ToListAsync();

            Assert.Equal(2, notifications.Count);
            Assert.Contains(notifications, n => n.RelatedEntityId == contract1.Id);
            Assert.Contains(notifications, n => n.RelatedEntityId == contract2.Id);
            Assert.All(notifications, n => Assert.Contains("Contract Expiring Soon", n.Title));
            Assert.All(notifications, n => Assert.False(n.IsRead));

            _mockEmailService.Verify(es => es.SendEmailAsync(
                tenant.Email,
                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
                It.IsAny<string>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task Handle_TenantNotFound_ShouldSkipNotificationAndEmail()
        {
            var tenantId = Guid.NewGuid().ToString();
            var owner = await _dataBuilder.CreateOwnerAsync();
            var branch = await _dataBuilder.CreateBranchAsync(owner.Id);
            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Active",
                EndDate = DateTime.UtcNow.AddDays(3),
                TenantId = tenantId,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1000,
                DepositAmount = 2000,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };

            await _dataBuilder.CreateContractRelatedDataAsync(tenantId, branch, contract);

            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            await _handler.Handle(command, CancellationToken.None);

            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);

            Assert.Null(notification);
            _mockEmailService.Verify(es => es.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
                Times.Never());
        }

        [Fact]
        public async Task Handle_ExistingNotification_ShouldNotCreateDuplicate()
        {
            var tenant = await _dataBuilder.CreateTenantAsync("test@domain.com");
            var owner = await _dataBuilder.CreateOwnerAsync();
            var branch = await _dataBuilder.CreateBranchAsync(owner.Id);
            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Active",
                EndDate = DateTime.UtcNow.AddDays(3),
                TenantId = tenant.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1000,
                DepositAmount = 2000,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };

            await _dataBuilder.CreateContractRelatedDataAsync(tenant.Id, branch, contract);

            var existingNotification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = tenant.Id,
                RelatedEntityId = contract.Id,
                RelatedEntityType = "Contract",
                CreatedAt = DateTime.UtcNow,
                Title = "Contract Expiring Soon",
                Message = $"Your contract {contract.Id} is expiring on {contract.EndDate:yyyy-MM-dd}.",
                IsRead = false
            };
            await _dbContext.Notifications.AddAsync(existingNotification);
            await _dbContext.SaveChangesAsync();

            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            await _handler.Handle(command, CancellationToken.None);

            var notifications = await _dbContext.Notifications
                .Where(n => n.UserId == tenant.Id && n.RelatedEntityId == contract.Id)
                .ToListAsync();

            Assert.Single(notifications);
            Assert.Equal(existingNotification.Id, notifications[0].Id);
            _mockEmailService.Verify(es => es.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
                Times.Never());
        }

        [Fact]
        public async Task Handle_NonActiveContract_ShouldNotCreateNotification()
        {
            var tenant = await _dataBuilder.CreateTenantAsync("test@domain.com");
            var owner = await _dataBuilder.CreateOwnerAsync();
            var branch = await _dataBuilder.CreateBranchAsync(owner.Id);
            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                Status = "Expired",
                EndDate = DateTime.UtcNow.AddDays(3),
                TenantId = tenant.Id,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                RentAmount = 1000,
                DepositAmount = 2000,
                Terms = "Standard terms",
                TenantRoom = new TenantRoom
                {
                    Room = new Room
                    {
                        Id = Guid.NewGuid(),
                        Name = "A101",
                        Location = "Building A",
                        Status = Room.RoomStatus.Occupied,
                        Notes = "Room notes"
                    }
                }
            };

            await _dataBuilder.CreateContractRelatedDataAsync(tenant.Id, branch, contract);

            var command = new CheckExpiringContractsCommand
            {
                CheckDate = DateTime.UtcNow.Date
            };

            await _handler.Handle(command, CancellationToken.None);

            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.UserId == tenant.Id && n.RelatedEntityId == contract.Id);

            Assert.Null(notification);
            _mockEmailService.Verify(es => es.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
                Times.Never());
        }
    }
}

//using EffiAP.Domain.Entities;
//using EffiAP.Infrastructure.EntityModels;
//using EffiAP.Infrastructure.IRepositories;
//using EffiAP.Infrastructure.Repositories;
//using EffiRent.Application.Commands.ContractCommand;
//using EffiRent.Application.Handlers.ContractHandler;
//using EffiRent.Application.Services.Email;
//using EffiRent.Domain.Entities;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.Data.Sqlite;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Moq;
//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Xunit;

//namespace EffiRent.Application.Tests.Integration.Handlers.ContractHandler
//{
//    public class CheckExpiringContractsHandlerIntegrationTests : IDisposable
//    {
//        private readonly IServiceScope _scope;
//        private readonly EffiRentContext _dbContext;
//        private readonly UserManager<IdentityUser> _userManager;
//        private readonly Mock<IEmailService> _mockEmailService;
//        private readonly IConfiguration _configuration;
//        private readonly IUnitOfWork _unitOfWork;
//        private readonly CheckExpiringContractsHandler _handler;
//        private readonly SqliteConnection _connection;

//        public CheckExpiringContractsHandlerIntegrationTests()
//        {
//            _connection = new SqliteConnection("DataSource=:memory:");
//            _connection.Open();

//            var services = new ServiceCollection();

//            services.AddLogging(builder => builder.AddConsole()); // Có thể dùng AddConsole hoặc AddDebug


//            services.AddDbContext<EffiRentContext>(options =>
//                options.UseSqlite(_connection));
//            services.AddScoped<IUnitOfWork, UnitOfWork>();
//            services.AddIdentity<IdentityUser, IdentityRole>()
//                .AddEntityFrameworkStores<EffiRentContext>()
//                .AddDefaultTokenProviders();

//            var serviceProvider = services.BuildServiceProvider();
//            _scope = serviceProvider.CreateScope();

//            _dbContext = _scope.ServiceProvider.GetRequiredService<EffiRentContext>();
//            _dbContext.Database.EnsureCreated();

//            _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

//            _mockEmailService = new Mock<IEmailService>();
//            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
//                            .Returns(Task.CompletedTask);

//            _configuration = new ConfigurationBuilder().Build();

//            _unitOfWork = _scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

//            _handler = new CheckExpiringContractsHandler(
//                _unitOfWork,
//                _mockEmailService.Object,
//                _userManager,
//                _configuration);
//        }

//        public void Dispose()
//        {
//            _dbContext.Dispose();
//            _connection.Close();
//            _connection.Dispose();
//            _scope.Dispose();
//        }

//        private async Task SeedDataAsync(IdentityUser tenant, Contract contract)
//        {
//            await _userManager.CreateAsync(tenant);

//            var owner = new IdentityUser
//            {
//                Id = Guid.NewGuid().ToString(),
//                UserName = "owner@domain.com",
//                Email = "owner@domain.com"
//            };
//            await _userManager.CreateAsync(owner);

//            var branch = new Branch
//            {
//                BranchID = Guid.NewGuid(),
//                BranchName = "Chi nhánh 1",
//                Address = "123 Đường Nguyễn Thị Minh Khai, Quận 1, Hồ Chí Minh",
//                Phone = "0901234567",
//                Email = "branch1@example.com",
//                OwnerId = owner.Id
//            };
//            await _dbContext.Branch.AddAsync(branch);

//            var room = contract.TenantRoom.Room;
//            room.BranchID = branch.BranchID;
//            room.Notes = "Room notes";
//            await _dbContext.Room.AddAsync(room);

//            var tenantRoom = contract.TenantRoom;
//            tenantRoom.TenantRoomID = Guid.NewGuid();
//            tenantRoom.TenantID = tenant.Id;
//            tenantRoom.RoomID = room.Id;
//            tenantRoom.StartDate = DateTime.UtcNow.AddMonths(-6);
//            await _dbContext.TenantRoom.AddAsync(tenantRoom);

//            contract.TenantRoomId = tenantRoom.TenantRoomID;
//            await _dbContext.Contracts.AddAsync(contract);

//            await _dbContext.SaveChangesAsync();
//        }

//        // Builder để tạo dữ liệu kiểm thử
//        private class TestDataBuilder
//        {
//            private readonly EffiRentContext _dbContext;
//            private readonly UserManager<IdentityUser> _userManager;

//            public TestDataBuilder(EffiRentContext dbContext, UserManager<IdentityUser> userManager)
//            {
//                _dbContext = dbContext;
//                _userManager = userManager;
//            }

//            public async Task<IdentityUser> CreateTenantAsync(string email = null)
//            {
//                var tenantId = Guid.NewGuid().ToString();
//                var userName = email ?? $"tenant_{Guid.NewGuid()}@domain.com";
//                var tenant = new IdentityUser
//                {
//                    Id = tenantId,
//                    UserName = userName,
//                    Email = userName
//                };
//                await _userManager.CreateAsync(tenant);
//                return tenant;
//            }

//            public async Task<IdentityUser> CreateOwnerAsync()
//            {
//                var ownerId = Guid.NewGuid().ToString();
//                var userName = $"owner_{Guid.NewGuid()}@domain.com";
//                var owner = new IdentityUser
//                {
//                    Id = ownerId,
//                    UserName = userName,
//                    Email = userName
//                };
//                await _userManager.CreateAsync(owner);
//                return owner;
//            }

//            public async Task<Branch> CreateBranchAsync(string ownerId)
//            {
//                var branch = new Branch
//                {
//                    BranchID = Guid.NewGuid(),
//                    BranchName = "Chi nhánh 1",
//                    Address = "123 Đường Nguyễn Thị Minh Khai, Quận 1, Hồ Chí Minh",
//                    Phone = "0901234567",
//                    Email = "branch1@example.com",
//                    OwnerId = ownerId
//                };
//                await _dbContext.Branch.AddAsync(branch);
//                await _dbContext.SaveChangesAsync();
//                return branch;
//            }

//            public async Task<(Room, TenantRoom, Contract)> CreateContractRelatedDataAsync(
//                string tenantId, Branch branch, Contract contract)
//            {
//                var room = contract.TenantRoom.Room;
//                room.BranchID = branch.BranchID;
//                room.Notes = "Room notes";
//                await _dbContext.Room.AddAsync(room);

//                var tenantRoom = contract.TenantRoom;
//                tenantRoom.TenantRoomID = Guid.NewGuid();
//                tenantRoom.TenantID = tenantId;
//                tenantRoom.RoomID = room.Id;
//                tenantRoom.StartDate = DateTime.UtcNow.AddMonths(-6);
//                await _dbContext.TenantRoom.AddAsync(tenantRoom);

//                contract.TenantRoomId = tenantRoom.TenantRoomID;
//                await _dbContext.Contracts.AddAsync(contract);

//                await _dbContext.SaveChangesAsync();
//                return (room, tenantRoom, contract);
//            }
//        }

//        [Fact]
//        public async Task Handle_NenGuiEmailVaTaoThongBao_ChoHopDongSapHetHan()
//        {
//            var tenantId = Guid.NewGuid().ToString();
//            var tenant = new IdentityUser { Id = tenantId, UserName = "test2@domain.com", Email = "test@domain.com" };
//            var contract = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Active",
//                EndDate = DateTime.UtcNow.AddDays(3),
//                TenantId = tenantId,
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1000,
//                DepositAmount = 2000,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A101",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };

//            await SeedDataAsync(tenant, contract);

//            var command = new CheckExpiringContractsCommand
//            {
//                CheckDate = DateTime.UtcNow.Date
//            };

//            await _handler.Handle(command, CancellationToken.None);

//            var notification = await _dbContext.Notifications
//                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);

//            Assert.NotNull(notification);
//            Assert.Equal(tenantId, notification.UserId);
//            Assert.Contains("Contract Expiring Soon", notification.Title);
//            Assert.False(notification.IsRead);

//            _mockEmailService.Verify(es => es.SendEmailAsync(
//                tenant.Email,
//                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
//                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
//                Times.Once());
//        }

//        [Fact]
//        public async Task Handle_WhenEmailFails_ShouldRollbackTransaction()
//        {
//            var tenantId = Guid.NewGuid().ToString();
//            var tenant = new IdentityUser { Id = tenantId, UserName = "test2@domain.com", Email = "test@domain.com" };
//            var contract = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Active",
//                EndDate = DateTime.UtcNow.AddDays(3),
//                TenantId = tenantId,
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1000,
//                DepositAmount = 2000,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A101",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };

//            await SeedDataAsync(tenant, contract);

//            var command = new CheckExpiringContractsCommand
//            {
//                CheckDate = DateTime.UtcNow.Date
//            };

//            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
//                            .ThrowsAsync(new Exception("Email failed"));

//            await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));

//            var notification = await _dbContext.Notifications
//                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);

//            Assert.Null(notification); // Notification không được lưu do rollback
//        }

//        [Fact]
//        public async Task Handle_NoContractsWithinSevenDays_ShouldNotCreateNotification()
//        {
//            var tenantId = Guid.NewGuid().ToString();
//            var tenant = new IdentityUser { Id = tenantId, UserName = "test2@domain.com", Email = "test@domain.com" };
//            var contract = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Active",
//                EndDate = DateTime.UtcNow.AddDays(10), // Ngoài 7 ngày
//                TenantId = tenantId,
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1000,
//                DepositAmount = 2000,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A101",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };

//            await SeedDataAsync(tenant, contract);

//            var command = new CheckExpiringContractsCommand
//            {
//                CheckDate = DateTime.UtcNow.Date
//            };

//            await _handler.Handle(command, CancellationToken.None);

//            var notification = await _dbContext.Notifications
//                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);

//            Assert.Null(notification); // Không tạo notification
//            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
//                                    Times.Never());
//        }

//        [Fact]
//        public async Task Handle_ContractExpiringToday_ShouldCreateNotificationAndSendEmail()
//        {
//            var tenantId = Guid.NewGuid().ToString();
//            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//            var contract = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Active",
//                EndDate = DateTime.UtcNow.Date, // Hết hạn hôm nay
//                TenantId = tenantId,
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1000,
//                DepositAmount = 2000,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A101",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };

//            await SeedDataAsync(tenant, contract);

//            var command = new CheckExpiringContractsCommand
//            {
//                CheckDate = DateTime.UtcNow.Date
//            };

//            await _handler.Handle(command, CancellationToken.None);

//            var notification = await _dbContext.Notifications
//                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);

//            Assert.NotNull(notification);
//            Assert.Equal(tenantId, notification.UserId);
//            Assert.Contains("Contract Expiring Soon", notification.Title);
//            Assert.False(notification.IsRead);

//            _mockEmailService.Verify(es => es.SendEmailAsync(
//                tenant.Email,
//                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
//                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
//                Times.Once());
//        }

//        [Fact]
//        public async Task Handle_ContractExpiringExactlySevenDays_ShouldCreateNotificationAndSendEmail()
//        {
//            var tenantId = Guid.NewGuid().ToString();
//            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//            var contract = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Active",
//                EndDate = DateTime.UtcNow.AddDays(7).Date, // Hết hạn đúng 7 ngày
//                TenantId = tenantId,
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1000,
//                DepositAmount = 2000,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A101",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };

//            await SeedDataAsync(tenant, contract);

//            var command = new CheckExpiringContractsCommand
//            {
//                CheckDate = DateTime.UtcNow.Date
//            };

//            await _handler.Handle(command, CancellationToken.None);

//            var notification = await _dbContext.Notifications
//                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);

//            Assert.NotNull(notification);
//            Assert.Equal(tenantId, notification.UserId);
//            Assert.Contains("Contract Expiring Soon", notification.Title);
//            Assert.False(notification.IsRead);

//            _mockEmailService.Verify(es => es.SendEmailAsync(
//                tenant.Email,
//                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
//                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
//                Times.Once());
//        }

//        [Fact]
//        public async Task Handle_MultipleContractsForSameTenant_ShouldCreateOneNotificationPerContract()
//        {
//            var tenantId = Guid.NewGuid().ToString();
//            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//            var contract1 = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Active",
//                EndDate = DateTime.UtcNow.AddDays(3),
//                TenantId = tenantId,
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1000,
//                DepositAmount = 2000,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A101",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };
//            var contract2 = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Active",
//                EndDate = DateTime.UtcNow.AddDays(5),
//                TenantId = tenantId,
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1200,
//                DepositAmount = 2400,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A102",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };

//            await SeedDataAsync(tenant, contract1);
//            await SeedDataAsync(tenant, contract2);

//            var command = new CheckExpiringContractsCommand
//            {
//                CheckDate = DateTime.UtcNow.Date
//            };

//            await _handler.Handle(command, CancellationToken.None);

//            var notifications = await _dbContext.Notifications
//                .Where(n => n.UserId == tenantId)
//                .ToListAsync();

//            Assert.Equal(2, notifications.Count);
//            Assert.Contains(notifications, n => n.RelatedEntityId == contract1.Id);
//            Assert.Contains(notifications, n => n.RelatedEntityId == contract2.Id);
//            Assert.All(notifications, n => Assert.Contains("Contract Expiring Soon", n.Title));
//            Assert.All(notifications, n => Assert.False(n.IsRead));

//            _mockEmailService.Verify(es => es.SendEmailAsync(
//                tenant.Email,
//                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
//                It.IsAny<string>()),
//                Times.Exactly(2));
//        }

//        [Fact]
//        public async Task Handle_TenantNotFound_ShouldSkipNotificationAndEmail()
//        {
//            var tenantId = Guid.NewGuid().ToString();
//            var contract = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Active",
//                EndDate = DateTime.UtcNow.AddDays(3),
//                TenantId = tenantId, // Tenant không tồn tại
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1000,
//                DepositAmount = 2000,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A101",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };

//            // Không tạo tenant trong UserManager
//            var owner = new IdentityUser
//            {
//                Id = Guid.NewGuid().ToString(),
//                UserName = "owner@domain.com",
//                Email = "owner@domain.com"
//            };
//            await _userManager.CreateAsync(owner);

//            var branch = new Branch
//            {
//                BranchID = Guid.NewGuid(),
//                BranchName = "Chi nhánh 1",
//                Address = "123 Đường Nguyễn Thị Minh Khai, Quận 1, Hồ Chí Minh",
//                Phone = "0901234567",
//                Email = "branch1@example.com",
//                OwnerId = owner.Id
//            };
//            await _dbContext.Branch.AddAsync(branch);

//            var room = contract.TenantRoom.Room;
//            room.BranchID = branch.BranchID;
//            room.Notes = "Room notes";
//            await _dbContext.Room.AddAsync(room);

//            var tenantRoom = contract.TenantRoom;
//            tenantRoom.TenantRoomID = Guid.NewGuid();
//            tenantRoom.TenantID = tenantId;
//            tenantRoom.RoomID = room.Id;
//            tenantRoom.StartDate = DateTime.UtcNow.AddMonths(-6);
//            await _dbContext.TenantRoom.AddAsync(tenantRoom);

//            contract.TenantRoomId = tenantRoom.TenantRoomID;
//            await _dbContext.Contracts.AddAsync(contract);

//            await _dbContext.SaveChangesAsync();

//            var command = new CheckExpiringContractsCommand
//            {
//                CheckDate = DateTime.UtcNow.Date
//            };

//            await _handler.Handle(command, CancellationToken.None);

//            var notification = await _dbContext.Notifications
//                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);

//            Assert.Null(notification);
//            _mockEmailService.Verify(es => es.SendEmailAsync(
//                It.IsAny<string>(),
//                It.IsAny<string>(),
//                It.IsAny<string>()),
//                Times.Never());
//        }

//        [Fact]
//        public async Task Handle_ExistingNotification_ShouldNotCreateDuplicate()
//        {
//            var tenantId = Guid.NewGuid().ToString();
//            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//            var contract = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Active",
//                EndDate = DateTime.UtcNow.AddDays(3),
//                TenantId = tenantId,
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1000,
//                DepositAmount = 2000,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A101",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };

//            await SeedDataAsync(tenant, contract);

//            // Tạo thông báo hiện có
//            var existingNotification = new Notification
//            {
//                Id = Guid.NewGuid(),
//                UserId = tenantId,
//                RelatedEntityId = contract.Id,
//                RelatedEntityType = "Contract",
//                CreatedAt = DateTime.UtcNow,
//                Title = "Contract Expiring Soon",
//                Message = $"Your contract {contract.Id} is expiring on {contract.EndDate:yyyy-MM-dd}.",
//                IsRead = false
//            };
//            await _dbContext.Notifications.AddAsync(existingNotification);
//            await _dbContext.SaveChangesAsync();

//            var command = new CheckExpiringContractsCommand
//            {
//                CheckDate = DateTime.UtcNow.Date
//            };

//            await _handler.Handle(command, CancellationToken.None);

//            var notifications = await _dbContext.Notifications
//                .Where(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id)
//                .ToListAsync();

//            Assert.Single(notifications); // Chỉ có 1 thông báo
//            Assert.Equal(existingNotification.Id, notifications[0].Id);
//            _mockEmailService.Verify(es => es.SendEmailAsync(
//                It.IsAny<string>(),
//                It.IsAny<string>(),
//                It.IsAny<string>()),
//                Times.Never());
//        }

//        [Fact]
//        public async Task Handle_NonActiveContract_ShouldNotCreateNotification()
//        {
//            var tenantId = Guid.NewGuid().ToString();
//            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//            var contract = new Contract
//            {
//                Id = Guid.NewGuid(),
//                Status = "Expired", // Không phải Active
//                EndDate = DateTime.UtcNow.AddDays(3),
//                TenantId = tenantId,
//                StartDate = DateTime.UtcNow.AddMonths(-6),
//                RentAmount = 1000,
//                DepositAmount = 2000,
//                Terms = "Standard terms",
//                TenantRoom = new TenantRoom
//                {
//                    Room = new Room
//                    {
//                        Id = Guid.NewGuid(),
//                        Name = "A101",
//                        Location = "Building A",
//                        Status = Room.RoomStatus.Occupied,
//                        Notes = "Room notes"
//                    }
//                }
//            };

//            await SeedDataAsync(tenant, contract);

//            var command = new CheckExpiringContractsCommand
//            {
//                CheckDate = DateTime.UtcNow.Date
//            };

//            await _handler.Handle(command, CancellationToken.None);

//            var notification = await _dbContext.Notifications
//                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);

//            Assert.Null(notification);
//            _mockEmailService.Verify(es => es.SendEmailAsync(
//                It.IsAny<string>(),
//                It.IsAny<string>(),
//                It.IsAny<string>()),
//                Times.Never());
//        }
//    }
//}

////using EffiAP.Domain.Entities;
////using EffiAP.Infrastructure.EntityModels;
////using EffiAP.Infrastructure.IRepositories;
////using EffiAP.Infrastructure.Repositories;
////using EffiRent.Application.Commands.ContractCommand;
////using EffiRent.Application.Handlers.ContractHandler;
////using EffiRent.Application.Services.Email;
////using EffiRent.Domain.Entities;
////using Microsoft.AspNetCore.Identity;
////using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
////using Microsoft.Data.Sqlite;
////using Microsoft.EntityFrameworkCore;
////using Microsoft.Extensions.Configuration;
////using Moq;
////using System;
////using System.Threading;
////using System.Threading.Tasks;
////using Xunit;

////namespace EffiRent.Application.Tests.Integration.Handlers.ContractHandler
////{
////    public class CheckExpiringContractsHandlerIntegrationTests : IDisposable
////    {
////        private readonly EffiRentContext _dbContext;
////        private readonly UserManager<IdentityUser> _userManager;
////        private readonly Mock<IEmailService> _mockEmailService;
////        private readonly IConfiguration _configuration;
////        private readonly IUnitOfWork _unitOfWork;
////        private readonly CheckExpiringContractsHandler _handler;
////        private readonly SqliteConnection _connection;

////        public CheckExpiringContractsHandlerIntegrationTests()
////        {
////            _connection = new SqliteConnection("DataSource=:memory:");
////            _connection.Open(); // Mở connection để giữ database in-memory

////            var options = new DbContextOptionsBuilder<EffiRentContext>()
////                .UseSqlite(_connection)
////                .Options;

////            _dbContext = new EffiRentContext(options);
////            _dbContext.Database.EnsureCreated(); // Tạo schema

////            var userStore = new UserStore<IdentityUser>(_dbContext);
////            _userManager = new UserManager<IdentityUser>(
////                userStore, null, null, null, null, null, null, null, null);

////            _mockEmailService = new Mock<IEmailService>();
////            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
////                            .Returns(Task.CompletedTask);

////            _configuration = new ConfigurationBuilder().Build();

////            _unitOfWork = new UnitOfWork(_dbContext);

////            _handler = new CheckExpiringContractsHandler(
////                _unitOfWork,
////                _mockEmailService.Object,
////                _userManager,
////                _configuration);
////        }

////        public void Dispose()
////        {
////            _dbContext.Dispose();
////            _connection.Close(); // Đóng connection để xóa dữ liệu
////            _connection.Dispose();
////        }

////        private async Task SeedDataAsync(IdentityUser tenant, Contract contract)
////        {
////            await _userManager.CreateAsync(tenant);

////            var owner = new IdentityUser
////            {
////                Id = Guid.NewGuid().ToString(),
////                UserName = "owner@domain.com",
////                Email = "owner@domain.com"
////            };
////            await _userManager.CreateAsync(owner);

////            var branch = new Branch
////            {
////                BranchID = Guid.NewGuid(),
////                BranchName = "Chi nhánh 1",
////                Address = "123 Đường Nguyễn Thị Minh Khai, Quận 1, Hồ Chí Minh",
////                Phone = "0901234567",
////                Email = "branch1@example.com",
////                OwnerId = owner.Id
////            };
////            await _dbContext.Branch.AddAsync(branch);

////            var room = contract.TenantRoom.Room;
////            room.BranchID = branch.BranchID;
////            room.Notes = "Room notes";
////            await _dbContext.Room.AddAsync(room);

////            var tenantRoom = contract.TenantRoom;
////            tenantRoom.TenantRoomID = Guid.NewGuid();
////            tenantRoom.TenantID = tenant.Id;
////            tenantRoom.RoomID = room.Id;
////            tenantRoom.StartDate = DateTime.UtcNow.AddMonths(-6);
////            await _dbContext.TenantRoom.AddAsync(tenantRoom);

////            contract.TenantRoomId = tenantRoom.TenantRoomID;
////            await _dbContext.Contracts.AddAsync(contract);

////            await _dbContext.SaveChangesAsync();
////        }

////        [Fact]
////        public async Task Handle_NenGuiEmailVaTaoThongBao_ChoHopDongSapHetHan()
////        {
////            var tenantId = Guid.NewGuid().ToString();
////            var tenant = new IdentityUser { Id = tenantId, UserName = "test2@domain.com", Email = "test@domain.com" };
////            var contract = new Contract
////            {
////                Id = Guid.NewGuid(),
////                Status = "Active",
////                EndDate = DateTime.UtcNow.AddDays(3),
////                TenantId = tenantId,
////                StartDate = DateTime.UtcNow.AddMonths(-6),
////                RentAmount = 1000,
////                DepositAmount = 2000,
////                Terms = "Standard terms",
////                TenantRoom = new TenantRoom
////                {
////                    Room = new Room
////                    {
////                        Id = Guid.NewGuid(),
////                        Name = "A101",
////                        Location = "Building A",
////                        Status = Room.RoomStatus.Occupied,
////                        Notes = "Room notes"
////                    }
////                }
////            };

////            await SeedDataAsync(tenant, contract);

////            var command = new CheckExpiringContractsCommand
////            {
////                CheckDate = DateTime.UtcNow.Date
////            };

////            await _handler.Handle(command, CancellationToken.None);

////            var notification = await _dbContext.Notifications
////                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);

////            Assert.NotNull(notification);
////            Assert.Equal(tenantId, notification.UserId);
////            Assert.Contains("Contract Expiring Soon", notification.Title);
////            Assert.False(notification.IsRead);

////            _mockEmailService.Verify(es => es.SendEmailAsync(
////                tenant.Email,
////                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
////                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
////                Times.Once());
////        }
////    }
////}

////using EffiAP.Domain.Entities;
////using EffiAP.Infrastructure.EntityModels;
////using EffiAP.Infrastructure.IRepositories;
////using EffiAP.Infrastructure.Repositories;
////using EffiRent.Application.Commands.ContractCommand;
////using EffiRent.Application.Handlers.ContractHandler;
////using EffiRent.Application.Services.Email;
////using EffiRent.Domain.Entities;
////using Microsoft.AspNetCore.Identity;
////using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
////using Microsoft.EntityFrameworkCore;
////using Microsoft.Extensions.Configuration;
////using Moq;
////using System;
////using System.Linq;
////using System.Threading;
////using System.Threading.Tasks;
////using Xunit;

////namespace EffiRent.Application.Tests.Integration.Handlers.ContractHandler
////{
////    public class CheckExpiringContractsHandlerIntegrationTests : IDisposable
////    {
////        private readonly EffiRentContext _dbContext;
////        private readonly UserManager<IdentityUser> _userManager;
////        private readonly Mock<IEmailService> _mockEmailService;
////        private readonly IConfiguration _configuration;
////        private readonly IUnitOfWork _unitOfWork;
////        private readonly CheckExpiringContractsHandler _handler;

////        private const string ConnectionString = "Data Source=NHTRUNG;Initial Catalog=EffiHR_DevelopmentDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

////        public CheckExpiringContractsHandlerIntegrationTests()
////        {
////            var options = new DbContextOptionsBuilder<EffiRentContext>()
////                .UseSqlServer(ConnectionString)
////                .Options;

////            _dbContext = new EffiRentContext(options);
////            //_dbContext.Database.EnsureDeleted(); // Làm sạch trước khi tạo
////            _dbContext.Database.EnsureCreated(); // Tạo cơ sở dữ liệu nếu chưa tồn tại

////            var userStore = new UserStore<IdentityUser>(_dbContext);
////            _userManager = new UserManager<IdentityUser>(
////                userStore, null, null, null, null, null, null, null, null);

////            _mockEmailService = new Mock<IEmailService>();
////            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
////                            .Returns(Task.CompletedTask);

////            _configuration = new ConfigurationBuilder().Build();

////            _unitOfWork = new UnitOfWork(_dbContext);

////            _handler = new CheckExpiringContractsHandler(
////                _unitOfWork,
////                _mockEmailService.Object,
////                _userManager,
////                _configuration);
////        }

////        public void Dispose()
////        {
////            //_dbContext.Database.EnsureDeleted(); // Xóa cơ sở dữ liệu sau mỗi bài kiểm tra
////            _dbContext.Dispose();
////        }

////        private async Task SeedDataAsync(IdentityUser tenant, Contract contract)
////        {
////            await _userManager.CreateAsync(tenant);

////            var owner = new IdentityUser
////            {
////                Id = Guid.NewGuid().ToString(),
////                UserName = "owner@domain.com",
////                Email = "owner@domain.com"
////            };
////            await _userManager.CreateAsync(owner);

////            var branch = new Branch
////            {
////                BranchID = Guid.NewGuid(),
////                BranchName = "Chi nhánh 1",
////                Address = "123 Đường Nguyễn Thị Minh Khai, Quận 1, Hồ Chí Minh",
////                Phone = "0901234567",
////                Email = "branch1@example.com",
////                OwnerId = owner.Id
////            };
////            await _dbContext.Branch.AddAsync(branch);

////            var room = contract.TenantRoom.Room;
////            room.BranchID = branch.BranchID;
////            room.Notes = "Room notes";
////            await _dbContext.Room.AddAsync(room);

////            var tenantRoom = contract.TenantRoom;
////            tenantRoom.TenantRoomID = Guid.NewGuid();
////            tenantRoom.TenantID = tenant.Id;
////            tenantRoom.RoomID = room.Id;
////            tenantRoom.StartDate = DateTime.UtcNow.AddMonths(-6);
////            await _dbContext.TenantRoom.AddAsync(tenantRoom);

////            contract.TenantRoomId = tenantRoom.TenantRoomID;
////            await _dbContext.Contracts.AddAsync(contract);

////            await _dbContext.SaveChangesAsync();
////        }

////        [Fact]
////        public async Task Handle_NenGuiEmailVaTaoThongBao_ChoHopDongSapHetHan()
////        {
////            //var tenantId = "tenant123";
////            var tenantId = Guid.NewGuid().ToString();
////            var tenant = new IdentityUser { Id = tenantId, UserName = "test2@domain.com", Email = "test@domain.com" };
////            var contract = new Contract
////            {
////                Id = Guid.NewGuid(),
////                Status = "Active",
////                EndDate = DateTime.UtcNow.AddDays(3),
////                TenantId = tenantId,
////                StartDate = DateTime.UtcNow.AddMonths(-6),
////                RentAmount = 1000,
////                DepositAmount = 2000,
////                Terms = "Standard terms",
////                TenantRoom = new TenantRoom
////                {
////                    Room = new Room
////                    {
////                        Id = Guid.NewGuid(),
////                        Name = "A101",
////                        Location = "Building A",
////                        Status = Room.RoomStatus.Occupied,
////                        Notes = "Room notes"
////                    }
////                }
////            };

////            await SeedDataAsync(tenant, contract);

////            var command = new CheckExpiringContractsCommand
////            {
////                CheckDate = DateTime.UtcNow.Date
////            };

////            await _handler.Handle(command, CancellationToken.None);

////            //var notification = await _dbContext.Notifications
////            //    .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);
////            var notification = _unitOfWork.Repository
////                .Get<Notification>(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id).FirstOrDefault();

////            Assert.NotNull(notification);
////            Assert.Equal(tenantId, notification.UserId);
////            Assert.Contains("Contract Expiring Soon", notification.Title);
////            Assert.False(notification.IsRead);

////            _mockEmailService.Verify(es => es.SendEmailAsync(
////                tenant.Email,
////                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
////                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
////                Times.Once());
////        }

////        //[Fact]
////        //public async Task Handle_KhongNenGuiEmail_KhiKhongCoHopDongSapHetHan()
////        //{
////        //    var command = new CheckExpiringContractsCommand
////        //    {
////        //        CheckDate = DateTime.UtcNow.Date
////        //    };

////        //    await _handler.Handle(command, CancellationToken.None);

////        //    var notifications = await _dbContext.Notifications.ToListAsync();
////        //    Assert.Empty(notifications);
////        //    _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
////        //}

////        //[Fact]
////        //public async Task Handle_KhongNenGuiEmail_KhiDaCoThongBao()
////        //{
////        //    var tenantId = "tenant123";
////        //    var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
////        //    var contract = new Contract
////        //    {
////        //        Id = Guid.NewGuid(),
////        //        Status = "Active",
////        //        EndDate = DateTime.UtcNow.AddDays(3),
////        //        TenantId = tenantId,
////        //        StartDate = DateTime.UtcNow.AddMonths(-6),
////        //        RentAmount = 1000,
////        //        DepositAmount = 2000,
////        //        Terms = "Standard terms",
////        //        TenantRoom = new TenantRoom
////        //        {
////        //            Room = new Room
////        //            {
////        //                Id = Guid.NewGuid(),
////        //                Name = "A101",
////        //                Location = "Building A",
////        //                Status = Room.RoomStatus.Occupied,
////        //                Notes = "Room notes"
////        //            }
////        //        }
////        //    };
////        //    var existingNotification = new Notification
////        //    {
////        //        Id = Guid.NewGuid(),
////        //        UserId = tenantId,
////        //        RelatedEntityId = contract.Id,
////        //        RelatedEntityType = "Contract",
////        //        CreatedAt = DateTime.UtcNow,
////        //        Title = "Contract Expiring Soon",
////        //        Message = "Thông báo thử nghiệm",
////        //        IsRead = false
////        //    };

////        //    await SeedDataAsync(tenant, contract);
////        //    await _dbContext.Notifications.AddAsync(existingNotification);
////        //    await _dbContext.SaveChangesAsync();

////        //    var command = new CheckExpiringContractsCommand
////        //    {
////        //        CheckDate = DateTime.UtcNow.Date
////        //    };

////        //    await _handler.Handle(command, CancellationToken.None);

////        //    var notifications = await _dbContext.Notifications.ToListAsync();
////        //    Assert.Single(notifications);
////        //    _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
////        //}

////        //[Fact]
////        //public async Task Handle_NenBoQuaHopDongKhongHoatDong()
////        //{
////        //    var tenantId = "tenant123";
////        //    var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
////        //    var contract = new Contract
////        //    {
////        //        Id = Guid.NewGuid(),
////        //        Status = "Terminated",
////        //        EndDate = DateTime.UtcNow.AddDays(3),
////        //        TenantId = tenantId,
////        //        StartDate = DateTime.UtcNow.AddMonths(-6),
////        //        RentAmount = 1000,
////        //        DepositAmount = 2000,
////        //        Terms = "Standard terms",
////        //        TenantRoom = new TenantRoom
////        //        {
////        //            Room = new Room
////        //            {
////        //                Id = Guid.NewGuid(),
////        //                Name = "A101",
////        //                Location = "Building A",
////        //                Status = Room.RoomStatus.Occupied,
////        //                Notes = "Room notes"
////        //            }
////        //        }
////        //    };

////        //    await SeedDataAsync(tenant, contract);

////        //    var command = new CheckExpiringContractsCommand
////        //    {
////        //        CheckDate = DateTime.UtcNow.Date
////        //    };

////        //    await _handler.Handle(command, CancellationToken.None);

////        //    var notifications = await _dbContext.Notifications.ToListAsync();
////        //    Assert.Empty(notifications);
////        //    _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
////        //}

////        //[Fact]
////        //public async Task Handle_NenBoQuaHopDongNgoaiNguong()
////        //{
////        //    var tenantId = "tenant123";
////        //    var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
////        //    var contract = new Contract
////        //    {
////        //        Id = Guid.NewGuid(),
////        //        Status = "Active",
////        //        EndDate = DateTime.UtcNow.AddDays(10),
////        //        TenantId = tenantId,
////        //        StartDate = DateTime.UtcNow.AddMonths(-6),
////        //        RentAmount = 1000,
////        //        DepositAmount = 2000,
////        //        Terms = "Standard terms",
////        //        TenantRoom = new TenantRoom
////        //        {
////        //            Room = new Room
////        //            {
////        //                Id = Guid.NewGuid(),
////        //                Name = "A101",
////        //                Location = "Building A",
////        //                Status = Room.RoomStatus.Occupied,
////        //                Notes = "Room notes"
////        //            }
////        //        }
////        //    };

////        //    await SeedDataAsync(tenant, contract);

////        //    var command = new CheckExpiringContractsCommand
////        //    {
////        //        CheckDate = DateTime.UtcNow.Date
////        //    };

////        //    await _handler.Handle(command, CancellationToken.None);

////        //    var notifications = await _dbContext.Notifications.ToListAsync();
////        //    Assert.Empty(notifications);
////        //    _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
////        //}

////        //[Fact]
////        //public async Task Handle_NenXuLyNhieuHopDongSapHetHan()
////        //{
////        //    var tenants = Enumerable.Range(1, 10).Select(i => new IdentityUser
////        //    {
////        //        Id = $"tenant{i}",
////        //        UserName = $"tenant{i}@domain.com",
////        //        Email = $"tenant{i}@domain.com"
////        //    }).ToList();

////        //    var owner = new IdentityUser
////        //    {
////        //        Id = "126ffc7b-fe62-4f35-b32c-984c1bddfb4d",
////        //        UserName = "owner@domain.com",
////        //        Email = "owner@domain.com"
////        //    };
////        //    await _userManager.CreateAsync(owner);

////        //    var branch = new Branch
////        //    {
////        //        BranchID = Guid.NewGuid(),
////        //        BranchName = "Chi nhánh 1",
////        //        Address = "123 Đường Nguyễn Thị Minh Khai, Quận 1, Hồ Chí Minh",
////        //        Phone = "0901234567",
////        //        Email = "branch1@example.com",
////        //        OwnerId = owner.Id
////        //    };
////        //    await _dbContext.Branch.AddAsync(branch);

////        //    var rooms = Enumerable.Range(1, 10).Select(i => new Room
////        //    {
////        //        Id = Guid.NewGuid(),
////        //        Name = $"Room 10{i}",
////        //        Location = "Building A",
////        //        BranchID = branch.BranchID,
////        //        Status = Room.RoomStatus.Occupied,
////        //        Notes = $"Notes for Room 10{i}"
////        //    }).ToList();
////        //    await _dbContext.Room.AddRangeAsync(rooms);

////        //    var tenantRooms = tenants.Zip(rooms, (t, r) => new TenantRoom
////        //    {
////        //        TenantRoomID = Guid.NewGuid(),
////        //        TenantID = t.Id,
////        //        RoomID = r.Id,
////        //        StartDate = DateTime.UtcNow.AddMonths(-6)
////        //    }).ToList();
////        //    await _dbContext.TenantRoom.AddRangeAsync(tenantRooms);

////        //    var contracts = tenantRooms.Select((tr, i) => new Contract
////        //    {
////        //        Id = Guid.NewGuid(),
////        //        TenantId = tr.TenantID,
////        //        TenantRoomId = tr.TenantRoomID,
////        //        Status = "Active",
////        //        StartDate = DateTime.UtcNow.AddMonths(-6),
////        //        EndDate = i < 5 ? DateTime.UtcNow.AddDays(i + 3) : DateTime.UtcNow.AddDays(30),
////        //        RentAmount = 1000 + i * 100,
////        //        DepositAmount = 2000 + i * 200,
////        //        Terms = "Standard terms"
////        //    }).ToList();

////        //    foreach (var tenant in tenants)
////        //        await _userManager.CreateAsync(tenant);
////        //    await _dbContext.Contracts.AddRangeAsync(contracts);
////        //    await _dbContext.SaveChangesAsync();

////        //    var command = new CheckExpiringContractsCommand
////        //    {
////        //        CheckDate = DateTime.UtcNow.Date
////        //    };

////        //    await _handler.Handle(command, CancellationToken.None);

////        //    var notifications = await _dbContext.Notifications.ToListAsync();
////        //    Assert.Equal(5, notifications.Count);
////        //    foreach (var notification in notifications)
////        //    {
////        //        Assert.Contains("Contract Expiring Soon", notification.Title);
////        //        Assert.False(notification.IsRead);
////        //    }

////        //    _mockEmailService.Verify(es => es.SendEmailAsync(
////        //        It.IsAny<string>(),
////        //        It.Is<string>(s => s.Contains("Contract Expiring Soon")),
////        //        It.IsAny<string>()),
////        //        Times.Exactly(5));
////        //}

////        //[Fact]
////        //public async Task Handle_ShouldRollbackTransaction_OnEmailServiceFailure()
////        //{
////        //    var tenantId = "tenant123";
////        //    var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
////        //    var contract = new Contract
////        //    {
////        //        Id = Guid.NewGuid(),
////        //        Status = "Active",
////        //        EndDate = DateTime.UtcNow.AddDays(3),
////        //        TenantId = tenantId,
////        //        StartDate = DateTime.UtcNow.AddMonths(-6),
////        //        RentAmount = 1000,
////        //        DepositAmount = 2000,
////        //        Terms = "Standard terms",
////        //        TenantRoom = new TenantRoom
////        //        {
////        //            Room = new Room
////        //            {
////        //                Id = Guid.NewGuid(),
////        //                Name = "A101",
////        //                Location = "Building A",
////        //                Status = Room.RoomStatus.Occupied,
////        //                Notes = "Room notes"
////        //            }
////        //        }
////        //    };

////        //    await SeedDataAsync(tenant, contract);

////        //    // Giả lập lỗi từ EmailService
////        //    _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
////        //                    .ThrowsAsync(new Exception("Email service failed"));

////        //    var command = new CheckExpiringContractsCommand
////        //    {
////        //        CheckDate = DateTime.UtcNow.Date
////        //    };

////        //    // Kiểm tra rằng handler ném ngoại lệ
////        //    await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));

////        //    // Kiểm tra rằng không có thông báo nào được lưu (do rollback)
////        //    var notifications = await _dbContext.Notifications.ToListAsync();
////        //    Assert.Empty(notifications);
////        //}

////    }
////}

//////using EffiAP.Domain.Entities;
//////using EffiAP.Infrastructure.EntityModels;
//////using EffiAP.Infrastructure.IRepositories;
//////using EffiAP.Infrastructure.Repositories;
//////using EffiRent.Application.Commands.ContractCommand;
//////using EffiRent.Application.Handlers.ContractHandler;
//////using EffiRent.Application.Services.Email;
//////using EffiRent.Domain.Entities;
//////using Microsoft.AspNetCore.Identity;
//////using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
//////using Microsoft.EntityFrameworkCore;
//////using Microsoft.Extensions.Configuration;
//////using Moq;
//////using System;
//////using System.Linq;
//////using System.Threading;
//////using System.Threading.Tasks;
//////using Xunit;

//////namespace EffiRent.Application.Tests.Integration.Handlers.ContractHandler
//////{
//////    public class CheckExpiringContractsHandlerIntegrationTests : IDisposable
//////    {
//////        private readonly EffiRentContext _dbContext;
//////        private readonly UserManager<IdentityUser> _userManager;
//////        private readonly Mock<IEmailService> _mockEmailService;
//////        private readonly IConfiguration _configuration;
//////        private readonly IUnitOfWork _unitOfWork;
//////        private readonly CheckExpiringContractsHandler _handler;

//////        public CheckExpiringContractsHandlerIntegrationTests()
//////        {
//////            var options = new DbContextOptionsBuilder<EffiRentContext>()
//////                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
//////                .Options;

//////            _dbContext = new EffiRentContext(options);

//////            var userStore = new UserStore<IdentityUser>(_dbContext);
//////            _userManager = new UserManager<IdentityUser>(
//////                userStore, null, null, null, null, null, null, null, null);

//////            _mockEmailService = new Mock<IEmailService>();
//////            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
//////                            .Returns(Task.CompletedTask);

//////            _configuration = new ConfigurationBuilder().Build();

//////            _unitOfWork = new UnitOfWork(_dbContext);

//////            _handler = new CheckExpiringContractsHandler(
//////                _unitOfWork,
//////                _mockEmailService.Object,
//////                _userManager,
//////                _configuration);
//////        }

//////        public void Dispose()
//////        {
//////            _dbContext.Database.EnsureDeleted();
//////            _dbContext.Dispose();
//////        }

//////        private async Task SeedDataAsync(IdentityUser tenant, Contract contract)
//////        {
//////            await _userManager.CreateAsync(tenant);

//////            var owner = new IdentityUser
//////            {
//////                Id = "126ffc7b-fe62-4f35-b32c-984c1bddfb4d",
//////                UserName = "owner@domain.com",
//////                Email = "owner@domain.com"
//////            };
//////            await _userManager.CreateAsync(owner);

//////            var branch = new Branch
//////            {
//////                BranchID = Guid.NewGuid(),
//////                BranchName = "Chi nhánh 1",
//////                Address = "123 Đường Nguyễn Thị Minh Khai, Quận 1, Hồ Chí Minh",
//////                Phone = "0901234567",
//////                Email = "branch1@example.com",
//////                OwnerId = owner.Id
//////            };
//////            await _dbContext.Branch.AddAsync(branch);

//////            var room = contract.TenantRoom.Room;
//////            room.BranchID = branch.BranchID;
//////            room.Notes = "Room notes";
//////            await _dbContext.Room.AddAsync(room);

//////            var tenantRoom = contract.TenantRoom;
//////            tenantRoom.TenantRoomID = Guid.NewGuid();
//////            tenantRoom.TenantID = tenant.Id;
//////            tenantRoom.RoomID = room.Id;
//////            tenantRoom.StartDate = DateTime.UtcNow.AddMonths(-6);
//////            await _dbContext.TenantRoom.AddAsync(tenantRoom);

//////            contract.TenantRoomId = tenantRoom.TenantRoomID;
//////            await _dbContext.Contracts.AddAsync(contract);

//////            await _dbContext.SaveChangesAsync();
//////        }

//////        [Fact]
//////        public async Task Handle_NenGuiEmailVaTaoThongBao_ChoHopDongSapHetHan()
//////        {
//////            var tenantId = "tenant123";
//////            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//////            var contract = new Contract
//////            {
//////                Id = Guid.NewGuid(),
//////                Status = "Active",
//////                EndDate = DateTime.UtcNow.AddDays(3),
//////                TenantId = tenantId,
//////                StartDate = DateTime.UtcNow.AddMonths(-6),
//////                RentAmount = 1000,
//////                DepositAmount = 2000,
//////                Terms = "Standard terms",
//////                TenantRoom = new TenantRoom
//////                {
//////                    Room = new Room
//////                    {
//////                        Id = Guid.NewGuid(),
//////                        Name = "A101",
//////                        Location = "Building A",
//////                        Status = Room.RoomStatus.Occupied
//////                    }
//////                }
//////            };

//////            await SeedDataAsync(tenant, contract);

//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            await _handler.Handle(command, CancellationToken.None);

//////            var notification = await _dbContext.Notifications
//////                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);
//////            Assert.NotNull(notification);
//////            Assert.Equal(tenantId, notification.UserId);
//////            Assert.Contains("Contract Expiring Soon", notification.Title);
//////            Assert.False(notification.IsRead);

//////            _mockEmailService.Verify(es => es.SendEmailAsync(
//////                tenant.Email,
//////                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
//////                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
//////                Times.Once());
//////        }

//////        [Fact]
//////        public async Task Handle_KhongNenGuiEmail_KhiKhongCoHopDongSapHetHan()
//////        {
//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            await _handler.Handle(command, CancellationToken.None);

//////            var notifications = await _dbContext.Notifications.ToListAsync();
//////            Assert.Empty(notifications);
//////            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//////        }

//////        [Fact]
//////        public async Task Handle_KhongNenGuiEmail_KhiDaCoThongBao()
//////        {
//////            var tenantId = "tenant123";
//////            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//////            var contract = new Contract
//////            {
//////                Id = Guid.NewGuid(),
//////                Status = "Active",
//////                EndDate = DateTime.UtcNow.AddDays(3),
//////                TenantId = tenantId,
//////                StartDate = DateTime.UtcNow.AddMonths(-6),
//////                RentAmount = 1000,
//////                DepositAmount = 2000,
//////                Terms = "Standard terms",
//////                TenantRoom = new TenantRoom
//////                {
//////                    Room = new Room
//////                    {
//////                        Id = Guid.NewGuid(),
//////                        Name = "A101",
//////                        Location = "Building A",
//////                        Status = Room.RoomStatus.Occupied
//////                    }
//////                }
//////            };
//////            var existingNotification = new Notification
//////            {
//////                Id = Guid.NewGuid(),
//////                UserId = tenantId,
//////                RelatedEntityId = contract.Id,
//////                RelatedEntityType = "Contract",
//////                CreatedAt = DateTime.UtcNow,
//////                Title = "Contract Expiring Soon",
//////                Message = "Thông báo thử nghiệm",
//////                IsRead = false
//////            };

//////            await SeedDataAsync(tenant, contract);
//////            await _dbContext.Notifications.AddAsync(existingNotification);
//////            await _dbContext.SaveChangesAsync();

//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            await _handler.Handle(command, CancellationToken.None);

//////            var notifications = await _dbContext.Notifications.ToListAsync();
//////            Assert.Single(notifications);
//////            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//////        }

//////        [Fact]
//////        public async Task Handle_NenBoQuaHopDongKhongHoatDong()
//////        {
//////            var tenantId = "tenant123";
//////            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//////            var contract = new Contract
//////            {
//////                Id = Guid.NewGuid(),
//////                Status = "Terminated",
//////                EndDate = DateTime.UtcNow.AddDays(3),
//////                TenantId = tenantId,
//////                StartDate = DateTime.UtcNow.AddMonths(-6),
//////                RentAmount = 1000,
//////                DepositAmount = 2000,
//////                Terms = "Standard terms",
//////                TenantRoom = new TenantRoom
//////                {
//////                    Room = new Room
//////                    {
//////                        Id = Guid.NewGuid(),
//////                        Name = "A101",
//////                        Location = "Building A",
//////                        Status = Room.RoomStatus.Occupied
//////                    }
//////                }
//////            };

//////            await SeedDataAsync(tenant, contract);

//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            await _handler.Handle(command, CancellationToken.None);

//////            var notifications = await _dbContext.Notifications.ToListAsync();
//////            Assert.Empty(notifications);
//////            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//////        }

//////        [Fact]
//////        public async Task Handle_NenBoQuaHopDongNgoaiNguong()
//////        {
//////            var tenantId = "tenant123";
//////            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//////            var contract = new Contract
//////            {
//////                Id = Guid.NewGuid(),
//////                Status = "Active",
//////                EndDate = DateTime.UtcNow.AddDays(10),
//////                TenantId = tenantId,
//////                StartDate = DateTime.UtcNow.AddMonths(-6),
//////                RentAmount = 1000,
//////                DepositAmount = 2000,
//////                Terms = "Standard terms",
//////                TenantRoom = new TenantRoom
//////                {
//////                    Room = new Room
//////                    {
//////                        Id = Guid.NewGuid(),
//////                        Name = "A101",
//////                        Location = "Building A",
//////                        Status = Room.RoomStatus.Occupied
//////                    }
//////                }
//////            };

//////            await SeedDataAsync(tenant, contract);

//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            await _handler.Handle(command, CancellationToken.None);

//////            var notifications = await _dbContext.Notifications.ToListAsync();
//////            Assert.Empty(notifications);
//////            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//////        }

//////        [Fact]
//////        public async Task Handle_NenXuLyNhieuHopDongSapHetHan()
//////        {
//////            var tenants = Enumerable.Range(1, 10).Select(i => new IdentityUser
//////            {
//////                Id = $"tenant{i}",
//////                UserName = $"tenant{i}@domain.com",
//////                Email = $"tenant{i}@domain.com"
//////            }).ToList();

//////            var owner = new IdentityUser
//////            {
//////                Id = "126ffc7b-fe62-4f35-b32c-984c1bddfb4d",
//////                UserName = "owner@domain.com",
//////                Email = "owner@domain.com"
//////            };
//////            await _userManager.CreateAsync(owner);

//////            var branch = new Branch
//////            {
//////                BranchID = Guid.NewGuid(),
//////                BranchName = "Chi nhánh 1",
//////                Address = "123 Đường Nguyễn Thị Minh Khai, Quận 1, Hồ Chí Minh",
//////                Phone = "0901234567",
//////                Email = "branch1@example.com",
//////                OwnerId = owner.Id
//////            };
//////            await _dbContext.Branch.AddAsync(branch);

//////            var rooms = Enumerable.Range(1, 10).Select(i => new Room
//////            {
//////                Id = Guid.NewGuid(),
//////                Name = $"Room 10{i}",
//////                Location = "Building A",
//////                BranchID = branch.BranchID,
//////                Status = Room.RoomStatus.Occupied
//////            }).ToList();
//////            await _dbContext.Room.AddRangeAsync(rooms);

//////            var tenantRooms = tenants.Zip(rooms, (t, r) => new TenantRoom
//////            {
//////                TenantRoomID = Guid.NewGuid(),
//////                TenantID = t.Id,
//////                RoomID = r.Id,
//////                StartDate = DateTime.UtcNow.AddMonths(-6)
//////            }).ToList();
//////            await _dbContext.TenantRoom.AddRangeAsync(tenantRooms);

//////            var contracts = tenantRooms.Select((tr, i) => new Contract
//////            {
//////                Id = Guid.NewGuid(),
//////                TenantId = tr.TenantID,
//////                TenantRoomId = tr.TenantRoomID,
//////                Status = "Active",
//////                StartDate = DateTime.UtcNow.AddMonths(-6),
//////                EndDate = i < 5 ? DateTime.UtcNow.AddDays(i + 3) : DateTime.UtcNow.AddDays(30),
//////                RentAmount = 1000 + i * 100,
//////                DepositAmount = 2000 + i * 200,
//////                Terms = "Standard terms"
//////            }).ToList();

//////            foreach (var tenant in tenants)
//////                await _userManager.CreateAsync(tenant);
//////            await _dbContext.Contracts.AddRangeAsync(contracts);
//////            await _dbContext.SaveChangesAsync();

//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            await _handler.Handle(command, CancellationToken.None);

//////            var notifications = await _dbContext.Notifications.ToListAsync();
//////            Assert.Equal(5, notifications.Count);
//////            foreach (var notification in notifications)
//////            {
//////                Assert.Contains("Contract Expiring Soon", notification.Title);
//////                Assert.False(notification.IsRead);
//////            }

//////            _mockEmailService.Verify(es => es.SendEmailAsync(
//////                It.IsAny<string>(),
//////                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
//////                It.IsAny<string>()),
//////                Times.Exactly(5));
//////        }
//////    }
//////}

//////using EffiAP.Domain.Entities;
//////using EffiAP.Infrastructure.EntityModels;
//////using EffiAP.Infrastructure.IRepositories;
//////using EffiAP.Infrastructure.Repositories;
//////using EffiRent.Application.Commands.ContractCommand;
//////using EffiRent.Application.Handlers.ContractHandler;
//////using EffiRent.Application.Services.Email;
//////using EffiRent.Domain.Entities;
////////using EffiRent.Infrastructure.Data; // Thay bằng namespace DbContext thực tế của bạn
//////using Microsoft.AspNetCore.Identity;
//////using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
//////using Microsoft.EntityFrameworkCore;
//////using Microsoft.Extensions.Configuration;
//////using Moq;
//////using System;
//////using System.Linq;
//////using System.Threading;
//////using System.Threading.Tasks;
//////using Xunit;

//////namespace EffiRent.Application.Tests.Integration.Handlers.ContractHandler
//////{
//////    public class CheckExpiringContractsHandlerIntegrationTests : IDisposable
//////    {
//////        private readonly EffiRentContext _dbContext; // Thay bằng DbContext thực tế của bạn
//////        private readonly UserManager<IdentityUser> _userManager;
//////        private readonly Mock<IEmailService> _mockEmailService;
//////        private readonly IConfiguration _configuration;
//////        private readonly IUnitOfWork _unitOfWork; // Thay bằng triển khai IUnitOfWork thực tế của bạn
//////        private readonly CheckExpiringContractsHandler _handler;

//////        public CheckExpiringContractsHandlerIntegrationTests()
//////        {
//////            // Thiết lập cơ sở dữ liệu trong bộ nhớ
//////            var options = new DbContextOptionsBuilder<EffiRentContext>()
//////                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
//////                .Options;

//////            _dbContext = new EffiRentContext(options); // Thay bằng DbContext thực tế của bạn

//////            // Thiết lập UserManager với kho lưu trữ trong bộ nhớ
//////            var userStore = new UserStore<IdentityUser>(_dbContext);
//////            _userManager = new UserManager<IdentityUser>(
//////                userStore, null, null, null, null, null, null, null, null);

//////            // Giả lập dịch vụ email
//////            _mockEmailService = new Mock<IEmailService>();
//////            _mockEmailService.Setup(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
//////                            .Returns(Task.CompletedTask);

//////            // Giả lập cấu hình (nếu cần)
//////            _configuration = new ConfigurationBuilder().Build();

//////            // Thiết lập UnitOfWork (giả định sử dụng cùng DbContext)
//////            _unitOfWork = new UnitOfWork(_dbContext); // Thay bằng triển khai IUnitOfWork thực tế của bạn

//////            // Khởi tạo handler
//////            _handler = new CheckExpiringContractsHandler(
//////                _unitOfWork,
//////                _mockEmailService.Object,
//////                _userManager,
//////                _configuration);
//////        }

//////        public void Dispose()
//////        {
//////            _dbContext.Database.EnsureDeleted();
//////            _dbContext.Dispose();
//////        }

//////        private async Task SeedDataAsync(IdentityUser tenant, Contract contract)
//////        {
//////            // Thêm tenant vào cơ sở dữ liệu
//////            await _userManager.CreateAsync(tenant);

//////            // Thêm hợp đồng vào cơ sở dữ liệu
//////            await _dbContext.Contracts.AddAsync(contract);
//////            await _dbContext.SaveChangesAsync();
//////        }

//////        [Fact]
//////        public async Task Handle_NenGuiEmailVaTaoThongBao_ChoHopDongSapHetHan()
//////        {
//////            // Sẵn sàng
//////            var tenantId = "tenant123";
//////            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//////            var contract = new Contract
//////            {
//////                Id = Guid.NewGuid(),
//////                Status = "Active",
//////                EndDate = DateTime.UtcNow.AddDays(3),
//////                TenantId = tenantId,
//////                TenantRoom = new TenantRoom
//////                {
//////                    Room = new Room
//////                    {
//////                        Id = Guid.NewGuid(),
//////                        Name = "A101",
//////                        Location = "Building A",
//////                        Status = Room.RoomStatus.Occupied
//////                    }
//////                }
//////            };

//////            await SeedDataAsync(tenant, contract);

//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            // Thực hiện
//////            await _handler.Handle(command, CancellationToken.None);

//////            // Kiểm tra
//////            var notification = await _dbContext.Notifications
//////                .FirstOrDefaultAsync(n => n.UserId == tenantId && n.RelatedEntityId == contract.Id);
//////            Assert.NotNull(notification);
//////            Assert.Equal(tenantId, notification.UserId);
//////            Assert.Contains("Contract Expiring Soon", notification.Title);
//////            Assert.False(notification.IsRead);

//////            _mockEmailService.Verify(es => es.SendEmailAsync(
//////                tenant.Email,
//////                It.Is<string>(s => s.Contains("Contract Expiring Soon")),
//////                It.Is<string>(s => s.Contains(contract.Id.ToString()))),
//////                Times.Once());
//////        }

//////        [Fact]
//////        public async Task Handle_KhongNenGuiEmail_KhiKhongCoHopDongSapHetHan()
//////        {
//////            // Sẵn sàng
//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            // Thực hiện
//////            await _handler.Handle(command, CancellationToken.None);

//////            // Kiểm tra
//////            var notifications = await _dbContext.Notifications.ToListAsync();
//////            Assert.Empty(notifications);
//////            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//////        }

//////        [Fact]
//////        public async Task Handle_KhongNenGuiEmail_KhiDaCoThongBao()
//////        {
//////            // Sẵn sàng
//////            var tenantId = "tenant123";
//////            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//////            var contract = new Contract
//////            {
//////                Id = Guid.NewGuid(),
//////                Status = "Active",
//////                EndDate = DateTime.UtcNow.AddDays(3),
//////                TenantId = tenantId,
//////                TenantRoom = new TenantRoom
//////                {
//////                    Room = new Room
//////                    {
//////                        Id = Guid.NewGuid(),
//////                        Name = "A101",
//////                        Location = "Building A",
//////                        Status = Room.RoomStatus.Occupied
//////                    }
//////                }
//////            };
//////            var existingNotification = new Notification
//////            {
//////                Id = Guid.NewGuid(),
//////                UserId = tenantId,
//////                RelatedEntityId = contract.Id,
//////                RelatedEntityType = "Contract",
//////                CreatedAt = DateTime.UtcNow,
//////                Title = "Contract Expiring Soon",
//////                Message = "Thông báo thử nghiệm",
//////                IsRead = false
//////            };

//////            await SeedDataAsync(tenant, contract);
//////            await _dbContext.Notifications.AddAsync(existingNotification);
//////            await _dbContext.SaveChangesAsync();

//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            // Thực hiện
//////            await _handler.Handle(command, CancellationToken.None);

//////            // Kiểm tra
//////            var notifications = await _dbContext.Notifications.ToListAsync();
//////            Assert.Single(notifications); // Chỉ có thông báo hiện có
//////            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//////        }

//////        [Fact]
//////        public async Task Handle_NenBoQuaHopDongKhongHoatDong()
//////        {
//////            // Sẵn sàng
//////            var tenantId = "tenant123";
//////            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//////            var contract = new Contract
//////            {
//////                Id = Guid.NewGuid(),
//////                Status = "Terminated", // Hợp đồng không hoạt động
//////                EndDate = DateTime.UtcNow.AddDays(3),
//////                TenantId = tenantId,
//////                TenantRoom = new TenantRoom
//////                {
//////                    Room = new Room
//////                    {
//////                        Id = Guid.NewGuid(),
//////                        Name = "A101",
//////                        Location = "Building A",
//////                        Status = Room.RoomStatus.Occupied
//////                    }
//////                }
//////            };

//////            await SeedDataAsync(tenant, contract);

//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            // Thực hiện
//////            await _handler.Handle(command, CancellationToken.None);

//////            // Kiểm tra
//////            var notifications = await _dbContext.Notifications.ToListAsync();
//////            Assert.Empty(notifications);
//////            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//////        }

//////        [Fact]
//////        public async Task Handle_NenBoQuaHopDongNgoaiNguong()
//////        {
//////            // Sẵn sàng
//////            var tenantId = "tenant123";
//////            var tenant = new IdentityUser { Id = tenantId, UserName = "test@domain.com", Email = "test@domain.com" };
//////            var contract = new Contract
//////            {
//////                Id = Guid.NewGuid(),
//////                Status = "Active",
//////                EndDate = DateTime.UtcNow.AddDays(10), // Ngoài ngưỡng 7 ngày
//////                TenantId = tenantId,
//////                TenantRoom = new TenantRoom
//////                {
//////                    Room = new Room
//////                    {
//////                        Id = Guid.NewGuid(),
//////                        Name = "A101",
//////                        Location = "Building A",
//////                        Status = Room.RoomStatus.Occupied
//////                    }
//////                }
//////            };

//////            await SeedDataAsync(tenant, contract);

//////            var command = new CheckExpiringContractsCommand
//////            {
//////                CheckDate = DateTime.UtcNow.Date
//////            };

//////            // Thực hiện
//////            await _handler.Handle(command, CancellationToken.None);

//////            // Kiểm tra
//////            var notifications = await _dbContext.Notifications.ToListAsync();
//////            Assert.Empty(notifications);
//////            _mockEmailService.Verify(es => es.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
//////        }
//////    }
//////}