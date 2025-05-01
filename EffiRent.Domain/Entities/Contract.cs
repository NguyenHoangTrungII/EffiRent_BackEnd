//using EffiAP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Domain.Entities
{
    public class Contract
    {
        public Guid Id { get; set; }
        public string TenantId { get; set; } // ID của người thuê
        public Guid TenantRoomId { get; set; } // Liên kết với TenantRoom
        public DateTime StartDate { get; set; } // Ngày bắt đầu hợp đồng
        public DateTime EndDate { get; set; } // Ngày kết thúc hợp đồng
        public decimal RentAmount { get; set; } // Giá thuê hàng tháng
        public decimal DepositAmount { get; set; } // Tiền đặt cọc
        public string Terms { get; set; } // Điều khoản hợp đồng
        public string Status { get; set; } // Trạng thái hợp đồng (ví dụ: "Active", "Expired", "Terminated")


        //public string userId { get; set; }

        // Navigation properties
        public virtual TenantRoom TenantRoom { get; set; }
        //public virtual User Tenant { get; set; }
        public virtual ICollection<Payment> Payments { get; set; } // Các khoản thanh toán liên quan
    }
}
