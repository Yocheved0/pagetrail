using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PageTrail.Infrastructure.Persistence;

namespace PageTrail.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        services.AddDbContext<PageTrailDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<PageTrailDbContext>();
        return services;
    }
}
