using EffiAP.Infrastructure.EntityModels;
using EffiAP.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly EffiRentContext _context;

        public UnitOfWork(EffiRentContext context)
        {
            _context = context;
        }

        private readonly Dictionary<Type, object?> _repositories = new Dictionary<Type, object?>();

        public IGenericRepository Repository
        {
            get
            {
                var type = typeof(IGenericRepository);
                if (_repositories.ContainsKey(type))
                    return (IGenericRepository)_repositories[type];

                var repositoryInstance = new GenericRepository(_context);
                _repositories.Add(type, repositoryInstance);
                return repositoryInstance;
            }
        }

        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }

        public Task<bool> SaveEntities()
        {
            _context.SaveChanges();
            return Task.FromResult(true);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
        {
            //await _mediator.DispatchDomainEventsAsync(_context);
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task CommitTransactionAsync(IDbContextTransaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await RollbackTransaction();
                throw;
            }
            finally
            {
                Dispose();
            }
        }

        public async Task RollbackTransaction()
        {
            try
            {
                await _context.Database.RollbackTransactionAsync()!;
            }
            finally
            {
                Dispose();
            }
        }

        public async Task RollbackTransactionAsync(IDbContextTransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to rollback transaction.", ex);
            }
            finally
            {
                transaction.Dispose();
            }
        }

        public IDbContextTransaction BeginTransaction()
        {
            return _context.Database.BeginTransaction();
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }


        public DatabaseFacade Database()
        {
            return _context.Database;
        }

        public EffiRentContext DBContext()
        {
            return _context;
        }
    }

}
