using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Domain.SeedWork
{
    public interface IInjectableService { }
    public interface ITransientService : IInjectableService { }
    public interface IScopedService : IInjectableService { }
    public interface ISingletonService : IInjectableService { }
}
