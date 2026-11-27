using FrioTrack.Api.Shared.Application;
using FrioTrack.Api.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FrioTrack.Api.Iam.Application;

public sealed class AccountAdministration(FrioTrackDbContext db,RelationalWorkspaceService workspace) : CommandServices.IAccountAccessCommandService,QueryServices.IAccountAccessQueryService {
    public async Task<object> List(string authorization){var actor=await workspace.Account(authorization);if(actor.Role!="coordinator")throw new ApiError("forbidden",403);return await db.Users.AsNoTracking().Where(u=>u.OrganizationId==actor.OrganizationId).OrderBy(u=>u.Name).Select(u=>new{u.Id,u.Name,u.Email,u.Role,u.Enabled}).ToArrayAsync();}
    public async Task Update(string authorization,string id,string role,bool enabled){
        var actor=await workspace.Account(authorization);if(actor.Role!="coordinator")throw new ApiError("forbidden",403);
        if(role is not ("coordinator" or "cargo-client"))throw new ApiError("forbidden",403);
        await using var tx=await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.OrganizationId},0))");
        var target=await db.Users.SingleOrDefaultAsync(u=>u.Id==id&&u.OrganizationId==actor.OrganizationId)??throw new ApiError("notFound",404);
        if(target.Id==actor.Id&&(role!="coordinator"||!enabled))throw new ApiError("selfAccessChange");
        if(role!="cargo-client"&&await db.Shipments.AnyAsync(s=>s.CustomerId==id))throw new ApiError("assignedClientRole",409);
        var changed=target.Role!=role||target.Enabled!=enabled;target.SetAccess(role,enabled);
        if(changed)await db.Sessions.Where(s=>s.UserId==id).ExecuteDeleteAsync();
        await db.SaveChangesAsync();await tx.CommitAsync();
    }
}
