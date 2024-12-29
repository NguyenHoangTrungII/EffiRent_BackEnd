using EffiAP.Infrastructure.EntityModels;
using EffiAP.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Infrastructure.Repositories
{
    public class GenericRepository : IGenericRepository
    {
        private readonly EffiRentContext _context;

        public GenericRepository(EffiRentContext context)
        {
            _context = context;
        }

        public IQueryable<T> GetAll<T>() where T : class
        {
            return _context.Set<T>();
        }

        public async Task AddAsync<T>(T entity) where T : class
        {
            await _context.Set<T>().AddAsync(entity);
        }

        public async Task AddRangeAsync<T>(IEnumerable<T> entities) where T : class
        {
            await _context.Set<T>().AddRangeAsync(entities);
        }


        public async Task UpdateAsync<T>(T entity) where T : class
        {
            var dbSet = _context.Set<T>();
            var entityEntry = _context.Entry(entity);

            if (entityEntry.State == EntityState.Detached)
            {
                dbSet.Attach(entity);
            }

            entityEntry.State = EntityState.Modified;
        }

        public async Task DeleteAsync<T>(Expression<Func<T, bool>> predicate = null) where T : class
        {
            var dbSet = _context.Set<T>();
            List<T> entities;

            if (predicate == null)
            {
                entities = await dbSet.ToListAsync();
            }
            else
            {
                entities = await dbSet.Where(predicate).ToListAsync();
            }

            if (entities.Any())
            {
                dbSet.RemoveRange(entities);
                await _context.SaveChangesAsync();
            }
        }


        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync<T>(int id) where T : class
        {
            return await _context.Set<T>().FindAsync(id) != null;
        }

        public async Task<bool> SaveAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public IQueryable<T> Get<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return _context.Set<T>().Where(predicate);
        }

        public IQueryable<T> GetAllAsync<T>() where T : class
        {
            return _context.Set<T>();
        }


        public async Task<T> GetOneAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return await _context.Set<T>().FirstOrDefaultAsync(predicate);
        }

        public IQueryable<T> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return _context.Set<T>().Where(predicate);
        }
    }
}
