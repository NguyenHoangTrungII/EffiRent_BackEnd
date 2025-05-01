using EffiAP.Application.Services.Redis;
using EffiRent.Domain.Entities;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Infrastructure.IRepositories;
using EffiHR.Application.Services;
using EffiRent.Application.Services.Redis;
using EffiRent.Application.Services.Technician;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

public class TechnicianAssignmentService : ITechnicianAssignmentService
{
    private readonly IApplicationRoleRepository _applicationRoleRepository;
    private readonly IApplicationUserRoleRepository _applicationUserRoleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheRedis  _redisCache;
    private readonly ILogger<TechnicianAssignmentService> _logger;
    private const string AvailableTechniciansKey = "available_technicians";

    public TechnicianAssignmentService(
        IApplicationRoleRepository applicationRoleRepository,
        IApplicationUserRoleRepository applicationUserRoleRepository,
        IUnitOfWork unitOfWork,
        //IRedisService redisService,
        ICacheRedis redisCache,
        ILogger<TechnicianAssignmentService> logger)

    {
        _applicationRoleRepository = applicationRoleRepository;
        _applicationUserRoleRepository = applicationUserRoleRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _redisCache = redisCache;
        InitializeCache().GetAwaiter().GetResult(); // Khởi tạo cache
        
    }

    private async Task InitializeCache()
    {
        var existingTechnicians = await _redisCache.SetMembersAsync(AvailableTechniciansKey);
        if (existingTechnicians != null && existingTechnicians.Any())
        {
            _logger.LogInformation("Cache already initialized with {Count} technicians", existingTechnicians.Count());
            return;
        }

        var technicianRole = await _applicationRoleRepository.GetTechnicianRoleIdAsync();
        if (technicianRole == null)
        {
            _logger.LogError("Technician role does not exist.");
            return;
        }

        var technicianUserIds = await _applicationUserRoleRepository.GetTechniciansAsync(technicianRole);
        var availableTechnicians = await GetAvailableTechniciansAsync(technicianUserIds);

        foreach (var technicianId in availableTechnicians)
        {
            await _redisCache.SetAddAsync(AvailableTechniciansKey, technicianId);
        }

        _logger.LogInformation("Initialized cache with {Count} available technicians", availableTechnicians.Count);
    }

    public async Task<string> AssignTechnicianAsync(MaintenanceRequestCommandDTO request)
    {
        try
        {
            // Thử lấy từ Redis
            string technicianId = null;
            try
            {
                technicianId = await _redisCache.SetPopAsync(AvailableTechniciansKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to access Redis cache, falling back to database");
            }

            if (technicianId != null)
            {
                _logger.LogInformation("Assigned technician {TechnicianId} from Redis cache", technicianId);
                return technicianId;
            }

            // Lấy từ database nếu Redis trống
            var technicianRole = await _applicationRoleRepository.GetTechnicianRoleIdAsync();
            if (technicianRole == null)
            {
                _logger.LogError("Technician role does not exist.");
                return null;
            }

            var technicianUserIds = await _applicationUserRoleRepository.GetTechniciansAsync(technicianRole);
            var availableTechnicians = await GetAvailableTechniciansAsync(technicianUserIds);

            if (!availableTechnicians.Any())
            {
                _logger.LogWarning("No available technicians found for request.");
                return null;
            }

            // Chọn kỹ thuật viên ngẫu nhiên
            technicianId = availableTechnicians.OrderBy(t => Guid.NewGuid()).First();
            _logger.LogInformation("Assigned technician {TechnicianId} from database", technicianId);

            // Đồng bộ cache
            try
            {
                await _redisCache.KeyDeleteAsync(AvailableTechniciansKey);
                foreach (var techId in availableTechnicians.Where(t => t != technicianId))
                {
                    await _redisCache.SetAddAsync(AvailableTechniciansKey, techId);
                }
                _logger.LogInformation("Updated cache with {Count} available technicians", availableTechnicians.Count - 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update Redis cache");
            }

            return technicianId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning technician for request");
            throw;
        }
    }

    public async Task MarkTechnicianAvailable(string technicianId)
    {
        try
        {
            await _redisCache.SetAddAsync(AvailableTechniciansKey, technicianId);
            _logger.LogInformation("Technician {TechnicianId} marked as available", technicianId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark technician {TechnicianId} as available in Redis", technicianId);
        }
    }

    public async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
    {
        var availableTechnicians = new ConcurrentBag<string>();
        var totalItems = technicianUserIds.Count;
        int pageSize = 4;

        var multiProcess = new MultiProcess();

        var pendingTechnicians = await _unitOfWork.Repository.Get<MaintenanceRequest>(
            req => req.Status == "Assigned" && req.TechnicianId != null)
            .Select(req => req.TechnicianId)
            .Distinct()
            .ToListAsync();

        var pendingRequestSet = new HashSet<string>(pendingTechnicians);

        await multiProcess.ExecuteHandler(pageSize, totalItems, async (skip, pageSize, threadIndex) =>
        {
            var currentBatch = technicianUserIds.Skip(skip).Take(pageSize).ToList();
            foreach (var technicianId in currentBatch)
            {
                if (!pendingRequestSet.Contains(technicianId))
                {
                    availableTechnicians.Add(technicianId);
                }
            }
        });

        var result = availableTechnicians.ToList();
        result.Sort();
        return result;
    }
}


//using EffiAP.Domain.Models;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiAP.Infrastructure.IRepositories;
//using EffiHR.Application.Services;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace EffiRent.Application.Services.Technician
//{
//    public class TechnicianAssignmentService : ITechnicianAssignmentService
//    {
//        private readonly IApplicationRoleRepository _applicationRoleRepository;
//        private readonly IApplicationUserRoleRepository _applicationUserRoleRepository;
//        private readonly IUnitOfWork _unitOfWork;
//        private readonly ILogger<TechnicianAssignmentService> _logger;

//        public TechnicianAssignmentService(
//            IApplicationRoleRepository applicationRoleRepository,
//            IApplicationUserRoleRepository applicationUserRoleRepository,
//            IUnitOfWork unitOfWork,
//            ILogger<TechnicianAssignmentService> logger)
//        {
//            _applicationRoleRepository = applicationRoleRepository;
//            _applicationUserRoleRepository = applicationUserRoleRepository;
//            _unitOfWork = unitOfWork;
//            _logger = logger;
//        }


//        public async Task<string> AssignTechnicianAsync(MaintenanceRequestCommandDTO request)
//        {
//            try
//            {
//                var technicianRole = await _applicationRoleRepository.GetTechnicianRoleIdAsync();
//                if (technicianRole == null)
//                {
//                    _logger.LogError("Technician role does not exist.");
//                    return null;
//                }

//                var technicianUserIds = await _applicationUserRoleRepository.GetTechniciansAsync(technicianRole);
//                var availableTechnicians = await GetAvailableTechniciansAsync(technicianUserIds);

//                if (!availableTechnicians.Any())
//                {
//                    _logger.LogWarning("No available technicians found for request.");
//                    return null;
//                }

//                // TODO: Thay bằng thuật toán gán thông minh (dựa trên tải công việc, kỹ năng)
//                var technicianId = availableTechnicians.OrderBy(t => Guid.NewGuid()).First(); // Random tạm thời
//                _logger.LogInformation("Assigned technician {TechnicianId} to request", technicianId);
//                return technicianId;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error assigning technician for request");
//                throw;
//            }
//        }

//        public async Task MarkTechnicianAvailable(string technicianId)
//        {
//            //var technician = await _unitOfWork.Repository.Get<Technician>()
//            //    .FirstOrDefaultAsync(t => t.Id == technicianId);
//            //if (technician != null)
//            //{
//            //    technician.IsAvailable = true;
//            //    await _unitOfWork.SaveChangesAsync();
//            //}

//            //await _redisService.SetAddAsync(AvailableTechniciansKey, technicianId);
//            _logger.LogInformation("Technician {TechnicianId} marked as available", technicianId);
//        }

//        public async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
//        {
//            var availableTechnicians = new ConcurrentBag<string>();
//            var totalItems = technicianUserIds.Count;
//            int pageSize = 4;

//            var multiProcess = new MultiProcess();

//            var pendingTechnicians = await _unitOfWork.Repository.Get<MaintenanceRequest>(
//                req => req.Status == "Assigned" && req.TechnicianId != null )
//                .Select(req => req.TechnicianId)
//                .Distinct()
//                .ToListAsync();

//            var pendingRequestSet = new HashSet<string>(pendingTechnicians);

//            await multiProcess.ExecuteHandler(pageSize, totalItems, async (skip, pageSize, threadIndex) =>
//            {
//                var currentBatch = technicianUserIds.Skip(skip).Take(pageSize).ToList();
//                foreach (var technicianId in currentBatch)
//                {
//                    if (!pendingRequestSet.Contains(technicianId))
//                    {
//                        availableTechnicians.Add(technicianId);
//                    }
//                }
//            });

//            var result = availableTechnicians.ToList();
//            result.Sort();
//            return result;
//        }

//        //public async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
//        //{
//        //    var availableTechnicians = new ConcurrentBag<string>();

//        //    // Lấy danh sách TechnicianId đang có yêu cầu Pending
//        //    //var pendingTechnicians = await _unitOfWork.Repository.Get<MaintenanceRequest>()
//        //    //    .Where(req => req.Status == "Pending" && req.TechnicianId != null)
//        //    //    .Select(req => req.TechnicianId)
//        //    //    .Distinct()
//        //    //    .ToListAsync();

//        //    var pendingTechnicians = _unitOfWork.Repository.Get<MaintenanceRequest>()
//        //        .Where(req => req.Status == "Assigned" && req.TechnicianId != null)
//        //        .Select(req => req.TechnicianId)
//        //        .Distinct()
//        //        .ToList();

//        //    var pendingRequestSet = new HashSet<string>(pendingTechnicians);

//        //    // Kiểm tra từng kỹ thuật viên
//        //    Parallel.ForEach(technicianUserIds, technicianId =>
//        //    {
//        //        if (!pendingRequestSet.Contains(technicianId))
//        //        {
//        //            availableTechnicians.Add(technicianId);
//        //        }
//        //    });

//        //    var result = availableTechnicians.ToList();
//        //    result.Sort(); // Sắp xếp để đảm bảo thứ tự nhất quán
//        //    return result;
//        //}

//        //private async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
//        //{
//        //    var pendingTechnicians = await _unitOfWork.Repository.Get<MaintenanceRequest>()
//        //        .Where(req => req.Status == "Pending" && req.TechnicianId != null)
//        //        .Select(req => req.TechnicianId)
//        //        .Distinct()
//        //        .ToListAsync(); 

//        //    return technicianUserIds.Except(pendingTechnicians).OrderBy(id => id).ToList();
//        //}
//    }
//}
