using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models
{
    public class SubscriberContext : DbContext
    {
        public SubscriberContext(DbContextOptions options): base(options) {}

        public DbSet<Subscriber> Subscribers { get; set; }
    }
}
