using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Infrastructure.IRepositories
{
    public interface IGenericRepository
    {
        IQueryable<T> GetAll<T>() where T : class;
        IQueryable<T> GetAllAsync<T>() where T : class;

        Task AddAsync<T>(T entity) where T : class;

        Task AddRangeAsync<T>(IEnumerable<T> entities) where T : class; 
        Task UpdateAsync<T>(T entity) where T : class;
        Task DeleteAsync<T>(Expression<Func<T, bool>> predicate) where T : class;
        //Task SaveChangesAsync();

        IQueryable<T> Get<T>(Expression<Func<T, bool>> predicate) where T : class;
        Task<T> GetOneAsync<T>(Expression<Func<T, bool>> predicate) where T : class;
        IQueryable<T> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class;

        Task<bool> ExistsAsync<T>(int id) where T : class;
        //Task<bool> SaveAsync();

    }
}
