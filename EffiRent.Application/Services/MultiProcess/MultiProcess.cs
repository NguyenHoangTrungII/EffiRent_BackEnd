using EffiHR.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiHR.Application.Services
{
    public class MultiProcess : IMultiProcess
    {
        public async Task ExecuteHandler(int pageSize, int totalItems, Func<int, int, int, Task> processAction)
        {
            int threadIndex = 0;
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < totalPages; i++)
            {
                int skip = i * pageSize;
                int currentThreadIndex = threadIndex++;

                // Add each task to a task list
                tasks.Add(Task.Run(() => processAction(skip, pageSize, currentThreadIndex)));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);
        }
    }


}
