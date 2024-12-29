using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiHR.Application.Interfaces
{
    public interface IMultiProcess
    {
        Task ExecuteHandler(int pageSize, int totalItems, Func<int, int, int, Task> processAction);
    }
}
