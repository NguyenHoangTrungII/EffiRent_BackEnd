namespace EffiAP.Domain.ViewModels
{
    public class PaginatedItemsViewModel<TEntity> where TEntity : class
    {

        public int Total { get; private set; }

        public IEnumerable<TEntity> Data { get; private set; }

        public PaginatedItemsViewModel(int total, IEnumerable<TEntity> data)
        {
            Total = total;
            Data = data;
        }

    }

    public class PaginatedObjectItemsViewModel<TEntity> where TEntity : class
    {

        public int Total { get; private set; }

        public TEntity? Data { get; private set; }


        public PaginatedObjectItemsViewModel(int total, TEntity? data)
        {
            Total = total;
            Data = data;
        }
    }
    
    public class PaginatedItems
    {
        public PaginatedItems(int pageIndex, int pageSize, long count, object data)
        {
            PageIndex = pageIndex + 1;
            PageSize = pageSize;
            Count = count;
            Data = data;
        }
        public int PageIndex { get; }
        public int PageSize { get; }
        public long Count { get; }
        public object Data { get; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < (double)Count / PageSize;
    }
}
