using Microsoft.EntityFrameworkCore;

namespace PageTrail.Infrastructure.Persistence;

public sealed class PageTrailDbContext(DbContextOptions<PageTrailDbContext> options) : DbContext(options);
