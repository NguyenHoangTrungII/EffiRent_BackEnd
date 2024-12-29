using EffiAP.Domain.SeedWork;
using EffiAP.Infrastructure.EntityModels;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Infrastructure.IRepositories
{
    public interface IUnitOfWork : IScopedService, IDisposable
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
        Task<bool> SaveEntities();

        IGenericRepository Repository { get; }
        Task CommitTransactionAsync(IDbContextTransaction transaction);
        Task RollbackTransaction();
        IDbContextTransaction BeginTransaction();
        DatabaseFacade Database();
        EffiRentContext DBContext();

    }
}
