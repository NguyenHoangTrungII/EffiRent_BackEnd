using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using EffiRent.Domain.Entities;
using EffiAP.Infrastructure.Repositories;
using EffiAP.Application.Commands.BranchCommand;
using EffiAP.Infrastructure.IRepositories;
using System.Configuration;
using FluentAssertions.Common;
using Microsoft.Extensions.Configuration;
using EffiAP.Application.Queries;
using System.Linq.Expressions;

namespace EffiAP.Application.Commands.BranchCommands
{
    public class CreateBranchCommandHandler : BaseQuery, IRequestHandler<CreateBranchCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateBranchCommandHandler(IUnitOfWork unitOfWork, IConfiguration configuration) : base(configuration)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
        {
            // Kiểm tra trạng thái hủy ngay đầu phương thức
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(request.BranchName))
            {
                throw new ArgumentException("BranchName cannot be empty.", nameof(request.BranchName));
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
            {
                throw new ArgumentException("Invalid email format.", nameof(request.Email));
            }

            if (string.IsNullOrWhiteSpace(request.OwnerId) || !Guid.TryParse(request.OwnerId, out _))
            {
                throw new ArgumentException("OwnerId must be a valid GUID.", nameof(request.OwnerId));
            }

            // Kiểm tra trùng lặp BranchName
            Expression<Func<Branch, bool>> predicate = b => b.BranchName == request.BranchName;
            var existingBranch = await _unitOfWork.Repository.GetOneAsync(predicate);
            if (existingBranch != null)
            {
                throw new InvalidOperationException("A branch with this name already exists.");
            }

            var branch = new Branch
            {
                BranchID = Guid.NewGuid(),
                OwnerId = request.OwnerId,
                BranchName = request.BranchName,
                Address = request.Address,
                Phone = request.Phone,
                Email = request.Email,
                Rooms = new List<Room>() // Có thể khởi tạo danh sách phòng trống
            };

            await _unitOfWork.Repository.AddAsync(branch);
            await _unitOfWork.SaveEntitiesAsync(cancellationToken);

            return branch.BranchID;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
