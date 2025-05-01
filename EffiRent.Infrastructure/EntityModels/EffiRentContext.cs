//using EffiAP.Domain.Entities;
//using EffiAP.Domain.Models;
using EffiRent.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Infrastructure.EntityModels
{
    public partial class EffiRentContext : IdentityDbContext<IdentityUser>
    {
        public EffiRentContext()
        {
        }

        public EffiRentContext(DbContextOptions<EffiRentContext> options)
        : base(options)
        {
        }

        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<CustomerFeedback> CustomerFeedbacks { get; set; }
        public DbSet<MaintenanceCategory> MaintenanceCategories { get; set; }

        public DbSet<Room> Room { get; set; }
        public DbSet<Branch> Branch { get; set; }

        public DbSet<Contract> Contracts { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Notification> Notifications { get; set; }


        public DbSet<TenantRoom> TenantRoom { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình cho bảng MaintenanceRequest
            modelBuilder.Entity<MaintenanceRequest>()
                .HasKey(m => m.Id); // Khóa chính

            modelBuilder.Entity<MaintenanceRequest>()
                .HasMany(m => m.Feedbacks) // Quan hệ 1-n với CustomerFeedback
                .WithOne(f => f.MaintenanceRequest) // Feedback có liên kết với MaintenanceRequest
                .HasForeignKey(f => f.MaintenanceRequestId) // Khóa ngoại ở bảng CustomerFeedback
                .OnDelete(DeleteBehavior.Cascade); // Xóa yêu cầu bảo trì sẽ xóa luôn feedback liên quan

            // Cấu hình cho bảng CustomerFeedback
            modelBuilder.Entity<CustomerFeedback>()
                .HasKey(f => f.Id); // Khóa chính

            modelBuilder.Entity<CustomerFeedback>()
                .HasOne(f => f.MaintenanceRequest) // Feedback liên kết với MaintenanceRequest
                .WithMany(m => m.Feedbacks) // Quan hệ nhiều phản hồi cho 1 yêu cầu bảo trì
                .HasForeignKey(f => f.MaintenanceRequestId); // Khóa ngoại trỏ đến bảng MaintenanceRequest

            // Cấu hình cho bảng MaintenanceCategory
            modelBuilder.Entity<MaintenanceCategory>()
                .HasKey(c => c.Id); // Khóa chính


            modelBuilder.Entity<MaintenanceRequest>()
               .HasOne(mr => mr.Room) // Một yêu cầu bảo trì thuộc về một phòng
               .WithMany(r => r.MaintenanceRequests) // Một phòng có nhiều yêu cầu bảo trì
               .HasForeignKey(mr => mr.RoomId) // Khóa ngoại từ MaintenanceRequest tới Room
               .OnDelete(DeleteBehavior.Restrict); // Xóa phòng sẽ không tự động xóa yêu cầu bảo trì liên quan

            modelBuilder.Entity<MaintenanceRequest>()
                .HasOne(mr => mr.Category)
                .WithMany(mc => mc.MaintenanceRequests)
                .HasForeignKey(mr => mr.CategoryId);

            // Cấu hình cho bảng Branch
            modelBuilder.Entity<Branch>()
                .HasKey(b => b.BranchID); // Khóa chính

            modelBuilder.Entity<Branch>()
                .HasMany(b => b.Rooms) // Một Branch có nhiều Room
                .WithOne(r => r.Branch) // Một Room thuộc về một Branch
                .HasForeignKey(r => r.BranchID) // Khóa ngoại từ Room tới Branch
                .OnDelete(DeleteBehavior.Cascade); // Xóa Branch sẽ xóa các Room liên quan

            // Cấu hình cho bảng Room
            modelBuilder.Entity<Room>()
                .HasKey(r => r.Id); // Khóa chính


            modelBuilder.Entity<MaintenanceRequest>()
                .HasMany(m => m.Images)
                .WithOne(i => i.MaintenanceRequest)
                .HasForeignKey(i => i.MaintenanceRequestId);

        }
    }
}
