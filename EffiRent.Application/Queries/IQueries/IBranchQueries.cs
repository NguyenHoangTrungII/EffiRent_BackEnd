using EffiAP.Domain.SeedWork;
using EffiAP.Domain.SeedWork;
using EffiAP.Domain.ViewModels.Branch;

namespace EffiAP.Application.Queries.IQueries
{
    public interface IBranchQuery : IScopedService
    {
        // Lấy danh sách chi nhánh theo từ khóa tìm kiếm, hỗ trợ phân trang
        //Task<object> GetBranchesBySearchKey(string? searchKey, int pageIndex = 0, int pageSize = 1000);

        // Lấy thông tin chi nhánh qua BranchID
        Task<BranchViewModel?> GetBranchById(Guid branchId);

        Task<List<BranchViewModel>> GetAllBranches();

    }
}
