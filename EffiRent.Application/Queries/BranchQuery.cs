using EffiAP.Infrastructure.Repositories;
using EffiAP.Domain.ViewModels;
using EffiAP.Application.Queries.IQueries;
using EffiAP.Domain.ViewModels.Branch;
using EffiAP.Infrastructure.IRepositories;
using Microsoft.Extensions.Configuration;
using FluentAssertions.Common;
using EffiAP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace EffiAP.Application.Queries
{
    public class BranchQuery : BaseQuery, IBranchQuery
    {
        private readonly IUnitOfWork _unitOfWork;

        public BranchQuery(IUnitOfWork unitOfWork, IConfiguration configuration) : base(configuration)
        {
            _unitOfWork = unitOfWork;
        }

        //public async Task<object> GetBranchesBySearchKey(string? searchKey, int pageIndex = 0, int pageSize = 1000)
        //{
        //    // Gọi repository để lấy danh sách chi nhánh theo từ khóa tìm kiếm và phân trang
        //    return await _unitOfWork.Repository.GetBranches(searchKey, pageIndex, pageSize);


        //}

        public async Task<BranchViewModel?> GetBranchById(Guid branchId)
        {
            // Lấy thông tin chi nhánh qua BranchID từ repository
            var branch = await _unitOfWork.Repository.GetOneAsync<Branch>(b=>b.BranchID == branchId);

            var branchById = new BranchViewModel()
            {
                BranchID = branchId,
                OwnerId = branch.OwnerId,
                BranchName = branch.BranchName,
                Address = branch.Address,
                Phone = branch.Phone,
                Email = branch.Email
            };

            return branch != null ? branchById : null;
        }

        public async Task<List<BranchViewModel>> GetAllBranches()
        {
            var branches = await _unitOfWork.Repository.GetAllAsync<Branch>().ToListAsync();

            var branchViewModels = branches.Select(branch => new BranchViewModel
            {
                BranchID = branch.BranchID,
                OwnerId = branch.OwnerId,
                BranchName = branch.BranchName,
                Address = branch.Address,
                Phone = branch.Phone,
                Email = branch.Email
            }).ToList();

            return branchViewModels;
        }

    }
}
