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
using EffiAP.Application.Commands.BranchCommand;
using EffiAP.Application.Commands.BranchCommands;

namespace EffiAP.Application.Tests.Integration.Commands.BranchCommands
{
    public class CreateBranchCommandHandlerIntegrationTests : IDisposable
    {

        private readonly IServiceScope _scope;
        private readonly EffiRentContext _dbContext;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;
        private readonly CreateBranchCommandHandler _handler;
        private readonly SqliteConnection _connection;
        //private readonly TestDataBuilder _dataBuilder;

        //public CreateBranchCommandHandlerIntegrationTests()
        //{
        //    var options = new DbContextOptionsBuilder<EffiRentContext>()
        //        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        //        .Options;
        //    _dbContext = new EffiRentContext(options);
        //    _unitOfWork = new UnitOfWork(_dbContext); // Giả định UnitOfWork triển khai IUnitOfWork với Repository
        //    _configuration = new ConfigurationBuilder().Build();
        //    _handler = new CreateBranchCommandHandler(_unitOfWork, _configuration);
        //}

        public CreateBranchCommandHandlerIntegrationTests()
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
            _handler = new CreateBranchCommandHandler(_unitOfWork, _configuration);


            //_dataBuilder = new TestDataBuilder(_dbContext, _userManager);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
            _scope.Dispose();
        }


        [Fact]
        public async Task Handle_ShouldCreateBranch_AndSaveToDatabase()
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

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var savedBranch = await _unitOfWork.Repository.GetByIdAsync<Branch>(result);
            Assert.NotNull(savedBranch);
            Assert.Equal(request.OwnerId, savedBranch.OwnerId);
            Assert.Equal(request.BranchName, savedBranch.BranchName);
            Assert.Equal(request.Address, savedBranch.Address);
            Assert.Equal(request.Phone, savedBranch.Phone);
            Assert.Equal(request.Email, savedBranch.Email);
            Assert.NotNull(savedBranch.Rooms);
            Assert.Empty(savedBranch.Rooms);
        }

        [Fact]
        public async Task Handle_ShouldThrowArgumentException_WhenBranchNameIsEmpty()
        {
            // Arrange
            var request = new CreateBranchCommand
            {
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "",
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "test@branch.com"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(request, CancellationToken.None));
            var branches = await _unitOfWork.Repository.GetByIdAsync<Branch>(Guid.NewGuid());
            Assert.Null(branches); // Không có chi nhánh nào được lưu
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
                Email = "invalid-email"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(request, CancellationToken.None));
            var branches = await _unitOfWork.Repository.GetByIdAsync<Branch>(Guid.NewGuid());
            Assert.Null(branches);
        }

        [Fact]
        public async Task Handle_ShouldThrowArgumentException_WhenOwnerIdIsInvalid()
        {
            // Arrange
            var request = new CreateBranchCommand
            {
                OwnerId = "invalid-guid",
                BranchName = "Test Branch",
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "test@branch.com"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(request, CancellationToken.None));
            var branches = await _unitOfWork.Repository.GetByIdAsync<Branch>(Guid.NewGuid());
            Assert.Null(branches);
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

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var savedBranch = await _unitOfWork.Repository.GetByIdAsync<Branch>(result);
            Assert.NotNull(savedBranch);
            Assert.Equal(maxLengthName, savedBranch.BranchName);
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
            var branches = await _unitOfWork.Repository.GetByIdAsync<Branch>(Guid.NewGuid());
            Assert.Null(branches);
        }

        [Fact]
        public async Task Handle_ShouldThrowException_WhenBranchNameExists()
        {
            // Arrange
            var existingBranch = new Branch
            {
                BranchID = Guid.NewGuid(),
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "Existing Branch",
                Address = "123 Test Street",
                Phone = "1234567890",
                Email = "existing@branch.com",
                Rooms = new List<Room>()
            };
            await _unitOfWork.Repository.AddAsync(existingBranch);
            await _unitOfWork.SaveEntitiesAsync();

            var request = new CreateBranchCommand
            {
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "Existing Branch",
                Address = "456 Another Street",
                Phone = "0987654321",
                Email = "new@branch.com"
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(request, CancellationToken.None));
            var branchCount = await _dbContext.Branch.CountAsync();
            Assert.Equal(1, branchCount);
        }

    }
}