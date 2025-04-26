using EffiAP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; set; }
        public string TenantId { get; set; } // ID của người thuê
        public Guid TenantRoomId { get; set; } // Liên kết với TenantRoom
        public decimal Amount { get; set; } // Số tiền thanh toán
        public DateTime PaymentDate { get; set; } // Ngày thanh toán
        public string PaymentMethod { get; set; } // Phương thức thanh toán (ví dụ: "Cash", "BankTransfer")
        public string Status { get; set; } // Trạng thái thanh toán (ví dụ: "Pending", "Completed", "Failed")
        public string Notes { get; set; } // Ghi chú (nếu có)

        //public string userId { get; set; }

        // Navigation properties
        public virtual TenantRoom TenantRoom { get; set; }
        //public virtual User Tenant { get; set; }
    }
}
