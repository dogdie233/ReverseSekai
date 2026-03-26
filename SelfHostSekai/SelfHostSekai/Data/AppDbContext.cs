using Microsoft.EntityFrameworkCore;
using SelfHostSekai.Models;

namespace SelfHostSekai.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
}