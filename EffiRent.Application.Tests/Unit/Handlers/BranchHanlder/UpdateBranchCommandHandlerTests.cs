using EffiAP.Application.Commands.BranchCommand;
using EffiRent.Domain.Entities;
using EffiAP.Infrastructure.IRepositories;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Tests.Unit.Handlers.BranchHanlder
{
    public class UpdateBranchCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository> _mockGenericRepo;
        private readonly UpdateBranchCommandHandler _handler;

        public UpdateBranchCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockGenericRepo = new Mock<IGenericRepository>();

            _mockUnitOfWork.Setup(u => u.Repository).Returns(_mockGenericRepo.Object);

            _handler = new UpdateBranchCommandHandler(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task Handle_ShouldUpdateBranch_AndReturnTrue()
        {
            // Arrange
            var branchId = Guid.NewGuid();
            var existingBranch = new Branch
            {
                BranchID = branchId,
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "Old Branch",
                Address = "Old Address",
                Phone = "000",
                Email = "old@email.com"
            };

            var request = new UpdateBranchCommand
            {
                BranchID = branchId,
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "New Branch",
                Address = "New Address",
                Phone = "123456",
                Email = "new@email.com"
            };

            _mockGenericRepo
                .Setup(repo => repo.GetOneAsync<Branch>(b => b.BranchID == request.BranchID))
                .ReturnsAsync(existingBranch);

            _mockUnitOfWork
                .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(request.BranchName, existingBranch.BranchName);
            Assert.Equal(request.Address, existingBranch.Address);
            Assert.Equal(request.Phone, existingBranch.Phone);
            Assert.Equal(request.Email, existingBranch.Email);
            Assert.Equal(request.OwnerId, existingBranch.OwnerId);

            _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturnFalse_WhenBranchNotFound()
        {
            // Arrange
            var request = new UpdateBranchCommand
            {
                BranchID = Guid.NewGuid(),
                OwnerId = Guid.NewGuid().ToString(),
                BranchName = "Not Exist",
                Address = "Somewhere",
                Phone = "000",
                Email = "notfound@email.com"
            };

            _mockGenericRepo
                .Setup(repo => repo.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Branch, bool>>>()))
                .ReturnsAsync((Branch)null); // Không tìm thấy

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.False(result);
            _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
