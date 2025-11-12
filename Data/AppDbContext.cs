using Microsoft.EntityFrameworkCore;
using BookQuoteAPI.Models;

namespace BookQuoteAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        {
        }

        public DbSet<Book> Books { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<Quote> Users { get; set; }
    }
}