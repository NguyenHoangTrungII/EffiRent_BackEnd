using EffiAP.Infrastructure.EntityModels;
using EffiAP.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EffiAP.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly EffiRentContext _context;
        private readonly Dictionary<Type, object> _repositories;

        public UnitOfWork(EffiRentContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repositories = new Dictionary<Type, object>();
        }

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

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        public bool SaveEntities()
        {
            _context.SaveChanges();
            return true;
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        public IDbContextTransaction BeginTransaction()
        {
            return _context.Database.BeginTransaction();
        }

        public async Task CommitAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            await transaction.CommitAsync(cancellationToken);
        }

        public void Commit(IDbContextTransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            transaction.Commit();
        }

        public async Task RollbackAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            await transaction.RollbackAsync(cancellationToken);
        }

        public void Rollback(IDbContextTransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            transaction.Rollback();
        }


        public DatabaseFacade Database => _context.Database;

        public EffiRentContext DbContext => _context;

        public void Dispose()
        {
            // Không dispose _context, để DI container hoặc lớp kiểm thử quản lý
            // Chỉ cleanup tài nguyên nội bộ nếu cần (hiện tại không có)
            GC.SuppressFinalize(this);
        }
    }
}

//using EffiAP.Infrastructure.EntityModels;
//using EffiAP.Infrastructure.IRepositories;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Infrastructure;
//using Microsoft.EntityFrameworkCore.Storage;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace EffiAP.Infrastructure.Repositories
//{
//    public class UnitOfWork : IUnitOfWork
//    {
//        private readonly EffiRentContext _context;

//        public UnitOfWork(EffiRentContext context)
//        {
//            _context = context;
//        }

//        private readonly Dictionary<Type, object?> _repositories = new Dictionary<Type, object?>();

//        public IGenericRepository Repository
//        {
//            get
//            {
//                var type = typeof(IGenericRepository);
//                if (_repositories.ContainsKey(type))
//                    return (IGenericRepository)_repositories[type];

//                var repositoryInstance = new GenericRepository(_context);
//                _repositories.Add(type, repositoryInstance);
//                return repositoryInstance;
//            }
//        }

//        public void Dispose()
//        {
//            _context.Dispose();
//            GC.SuppressFinalize(this);
//        }

//        public Task<bool> SaveEntities()
//        {
//            _context.SaveChanges();
//            return Task.FromResult(true);
//        }

//        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
//        {
//            return await _context.SaveChangesAsync(cancellationToken);
//        }

//        public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
//        {
//            //await _mediator.DispatchDomainEventsAsync(_context);
//            await _context.SaveChangesAsync(cancellationToken);

//            return true;
//        }

//        public async Task CommitTransactionAsync(IDbContextTransaction transaction)
//        {
//            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
//            try
//            {
//                await _context.SaveChangesAsync();
//                await transaction.CommitAsync();
//            }
//            catch
//            {
//                await RollbackTransaction();
//                throw;
//            }
//            finally
//            {
//                //Dispose();
//            }
//        }

//        public async Task RollbackTransaction()
//        {
//            try
//            {
//                await _context.Database.RollbackTransactionAsync()!;
//            }
//            finally
//            {
//                //Dispose();
//            }
//        }

//        public async Task RollbackTransactionAsync(IDbContextTransaction transaction)
//        {
//            if (transaction == null)
//                throw new ArgumentNullException(nameof(transaction));

//            try
//            {
//                await transaction.RollbackAsync();
//            }
//            catch (Exception ex)
//            {
//                throw new InvalidOperationException("Failed to rollback transaction.", ex);
//            }
//            //finally
//            //{
//            //    transaction.Dispose();
//            //}
//        }

//        public IDbContextTransaction BeginTransaction()
//        {
//            return _context.Database.BeginTransaction();
//        }

//        public async Task<IDbContextTransaction> BeginTransactionAsync()
//        {
//            return await _context.Database.BeginTransactionAsync();
//        }


//        public DatabaseFacade Database()
//        {
//            return _context.Database;
//        }

//        public EffiRentContext DBContext()
//        {
//            return _context;
//        }
//    }

//}
