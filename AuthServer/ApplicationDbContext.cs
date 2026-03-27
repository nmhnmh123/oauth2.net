using Microsoft.EntityFrameworkCore;

namespace AuthServer.Data
{
    // Model User
    public class AppUser
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; } // Demo nên lưu text thẳng, thực tế phải hash
        public string Role { get; set; }
    }

    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<AppUser> Users { get; set; }
    }
}
