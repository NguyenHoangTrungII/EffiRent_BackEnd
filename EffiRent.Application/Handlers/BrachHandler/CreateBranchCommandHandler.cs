using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using EffiAP.Domain.Entities;
using EffiAP.Infrastructure.Repositories;
using EffiAP.Application.Commands.BranchCommand;
using EffiAP.Infrastructure.IRepositories;
using System.Configuration;
using FluentAssertions.Common;
using Microsoft.Extensions.Configuration;
using EffiAP.Application.Queries;

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
    }
}
