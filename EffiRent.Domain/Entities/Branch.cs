using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Domain.Entities
{
    public class Branch
    {
        public Guid  BranchID { get; set; }

        public string OwnerId { get; set; }
        public string BranchName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }

        // Navigation property
        public ICollection<Room> Rooms { get; set; }
    }

}
