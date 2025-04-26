using EffiAP.Domain.Models;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Technician
{
    public class TechnicianAssignmentService : ITechnicianAssignmentService
    {
        private readonly IApplicationRoleRepository _applicationRoleRepository;
        private readonly IApplicationUserRoleRepository _applicationUserRoleRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TechnicianAssignmentService> _logger;

        public TechnicianAssignmentService(
            IApplicationRoleRepository applicationRoleRepository,
            IApplicationUserRoleRepository applicationUserRoleRepository,
            IUnitOfWork unitOfWork,
            ILogger<TechnicianAssignmentService> logger)
        {
            _applicationRoleRepository = applicationRoleRepository;
            _applicationUserRoleRepository = applicationUserRoleRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<string> AssignTechnicianAsync(MaintenanceRequestCommandDTO request)
        {
            try
            {
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

                // TODO: Thay bằng thuật toán gán thông minh (dựa trên tải công việc, kỹ năng)
                var technicianId = availableTechnicians.OrderBy(t => Guid.NewGuid()).First(); // Random tạm thời
                _logger.LogInformation("Assigned technician {TechnicianId} to request", technicianId);
                return technicianId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning technician for request");
                throw;
            }
        }

        public async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
        {
            var availableTechnicians = new ConcurrentBag<string>();

            // Lấy danh sách TechnicianId đang có yêu cầu Pending
            //var pendingTechnicians = await _unitOfWork.Repository.Get<MaintenanceRequest>()
            //    .Where(req => req.Status == "Pending" && req.TechnicianId != null)
            //    .Select(req => req.TechnicianId)
            //    .Distinct()
            //    .ToListAsync();

            var pendingTechnicians = _unitOfWork.Repository.Get<MaintenanceRequest>()
                .Where(req => req.Status == "Assigned" && req.TechnicianId != null)
                .Select(req => req.TechnicianId)
                .Distinct()
                .ToList();

            var pendingRequestSet = new HashSet<string>(pendingTechnicians);

            // Kiểm tra từng kỹ thuật viên
            Parallel.ForEach(technicianUserIds, technicianId =>
            {
                if (!pendingRequestSet.Contains(technicianId))
                {
                    availableTechnicians.Add(technicianId);
                }
            });

            var result = availableTechnicians.ToList();
            result.Sort(); // Sắp xếp để đảm bảo thứ tự nhất quán
            return result;
        }

        //private async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
        //{
        //    var pendingTechnicians = await _unitOfWork.Repository.Get<MaintenanceRequest>()
        //        .Where(req => req.Status == "Pending" && req.TechnicianId != null)
        //        .Select(req => req.TechnicianId)
        //        .Distinct()
        //        .ToListAsync(); 

        //    return technicianUserIds.Except(pendingTechnicians).OrderBy(id => id).ToList();
        //}
    }
}
