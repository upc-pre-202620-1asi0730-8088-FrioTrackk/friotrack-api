using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace FrioTrack.Api.Shared.Infrastructure;

public sealed class FrioTrackDesignTimeFactory : IDesignTimeDbContextFactory<FrioTrackDbContext> {
    public FrioTrackDbContext CreateDbContext(string[] args) {
        var connection = Environment.GetEnvironmentVariable("FRIOTRACK_DB") ?? "Host=127.0.0.1;Database=friotrack;Username=friotrack";
        return new FrioTrackDbContext(new DbContextOptionsBuilder<FrioTrackDbContext>().UseNpgsql(connection).Options);
    }
}
