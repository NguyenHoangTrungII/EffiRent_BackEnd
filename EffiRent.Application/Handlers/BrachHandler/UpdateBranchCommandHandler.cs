using EffiAP.Domain.Entities;
using EffiAP.Infrastructure.IRepositories;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EffiAP.Application.Commands.BranchCommand
{
    public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateBranchCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
        {
            // Lấy chi nhánh từ cơ sở dữ liệu
            var branch = await _unitOfWork.Repository.GetOneAsync<Branch>(b => b.BranchID == request.BranchID);

            // Kiểm tra xem chi nhánh có tồn tại không
            if (branch == null)
            {
                return false; // Trả về false nếu không tìm thấy chi nhánh
            }

            // Cập nhật các thuộc tính của chi nhánh
            branch.OwnerId = request.OwnerId;
            branch.BranchName = request.BranchName;
            branch.Address = request.Address;
            branch.Phone = request.Phone;
            branch.Email = request.Email;

            // Lưu thay đổi vào cơ sở dữ liệu
            await _unitOfWork.SaveChangesAsync();

            return true; // Trả về true nếu cập nhật thành công
        }
    }
}
