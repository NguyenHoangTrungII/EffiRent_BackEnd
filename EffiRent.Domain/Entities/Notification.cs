using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Domain.Entities
{
    public class Notification
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } // Người nhận thông báo
        public string Title { get; set; } // Tiêu đề thông báo
        public string Message { get; set; } // Nội dung thông báo
        public DateTime CreatedAt { get; set; } // Thời gian tạo
        public bool IsRead { get; set; } // Đã đọc hay chưa
        public string RelatedEntityType { get; set; } // Loại entity liên quan (ví dụ: "MaintenanceRequest", "Payment")
        public Guid? RelatedEntityId { get; set; } // ID của entity liên quan

        //public string userId { get; set; }

        // Navigation property
        //public virtual User User { get; set; }
    }
}
