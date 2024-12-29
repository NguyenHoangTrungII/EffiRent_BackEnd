using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EffiAP.Domain;

namespace EffiAP.Domain.ViewModels.Branch
{
    public class BranchViewModel
    {
        public Guid BranchID { get; set; }
        public string OwnerId { get; set; }
        public string BranchName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }

        //public BranchViewModel(Branch branch)
        //{
        //    BranchID = branch.BranchID;
        //    OwnerId = branch.OwnerId;
        //    BranchName = branch.BranchName;
        //    Address = branch.Address;
        //    Phone = branch.Phone;
        //    Email = branch.Email;
        //}
    }
}
