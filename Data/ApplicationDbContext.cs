using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MovieActorManager.Models;

namespace MovieActorManager.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<MovieActorManager.Models.Actor> Actor { get; set; } = default!;
        public DbSet<MovieActorManager.Models.Movie> Movie { get; set; } = default!;
        public DbSet<MovieActorManager.Models.MovieActor> MovieActor { get; set; } = default!;
    }
}
