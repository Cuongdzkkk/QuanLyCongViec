using Microsoft.EntityFrameworkCore;
// using TaskManagement.Domain.Entities; 

namespace TaskManagement.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Tạm thời để trống. 
        // Sau khi Dev 1 viết xong 25 file C# ở thư mục Domain, sẽ khai báo vào đây.
        // Ví dụ: public DbSet<User> Users { get; set; }
        // Ví dụ: public DbSet<Project> Projects { get; set; }
    }
}