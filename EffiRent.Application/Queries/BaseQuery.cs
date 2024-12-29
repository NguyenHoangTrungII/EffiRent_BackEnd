using Microsoft.Extensions.Configuration;

namespace EffiAP.Application.Queries;

public class BaseQuery
{
    public readonly IConfiguration Configuration;
    public readonly string ConnectionString;

    public BaseQuery(IConfiguration configuration)
    {
        Configuration = configuration;
        ConnectionString = Configuration["ConnectionString"];
    }
}
