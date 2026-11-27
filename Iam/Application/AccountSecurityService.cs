using System.Security.Cryptography;
using System.Text;
using FrioTrack.Api.Iam.Application.Ports;
using FrioTrack.Api.Iam.Domain;
using FrioTrack.Api.Shared.Application;
using FrioTrack.Api.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FrioTrack.Api.Iam.Application;

public sealed class AccountSecurityService(FrioTrackDbContext db, IPasswordHasher passwords,IRecoveryMail mail,IConfiguration configuration,ILogger<AccountSecurityService> logger) : CommandServices.IAccountSecurityCommandService {
    static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public bool RecoveryAvailable=>mail.IsConfigured && Uri.TryCreate(configuration["FRIOTRACK_RESET_URL"],UriKind.Absolute,out var url) && (url.Scheme=="https" || url.IsLoopback);
    public async Task RequestReset(string email,CancellationToken cancellationToken) {
        if(!RecoveryAvailable)throw new ApiError("recoveryUnavailable",503);
        if(string.IsNullOrWhiteSpace(email)||email.Length>140)throw new ApiError("invalidProfile");
        var account=await db.Users.SingleOrDefaultAsync(x=>x.Email==email.Trim().ToLowerInvariant()&&x.Enabled,cancellationToken);
        if(account is null)return;
        var now=DateTimeOffset.UtcNow;
        await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"recovery:"+account.Id},0))",cancellationToken);
        if(await db.PasswordResetTokens.AnyAsync(x=>x.UserId==account.Id && x.UsedAt==null && x.ExpiresAt>now.AddMinutes(14),cancellationToken)){await tx.CommitAsync(cancellationToken);return;}
        await db.PasswordResetTokens.Where(x=>x.UserId==account.Id&&x.UsedAt==null).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.UsedAt,now),cancellationToken);
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var entry=new PasswordResetToken{Id=Guid.NewGuid().ToString(),UserId=account.Id,TokenHash=Hash(token),ExpiresAt=now.AddMinutes(15)};
        db.PasswordResetTokens.Add(entry);await db.SaveChangesAsync(cancellationToken);await tx.CommitAsync(cancellationToken);
        var url=configuration["FRIOTRACK_RESET_URL"]!;url+=(url.Contains('?')?'&':'?')+"token="+Uri.EscapeDataString(token);
        try{await mail.Send(account.Email,url,cancellationToken);}
        catch(Exception error) when(error is System.Net.Mail.SmtpException or InvalidOperationException){entry.UsedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(cancellationToken);logger.LogWarning("Recovery mail delivery failed ({Type}); no token or address recorded.",error.GetType().Name);}
    }
    public async Task Reset(string token,string password,CancellationToken cancellationToken) {
        if(string.IsNullOrEmpty(token)||token.Length!=64)throw new ApiError("invalidResetToken");
        if(password.Length is <12 or >128)throw new ApiError("invalidPassword");
        await using var tx=await db.Database.BeginTransactionAsync(cancellationToken);
        var digest=Hash(token);
        var entry=await db.PasswordResetTokens.AsNoTracking().SingleOrDefaultAsync(x=>x.TokenHash==digest,cancellationToken);
        if(entry is null)throw new ApiError("invalidResetToken");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"recovery:"+entry.UserId},0))",cancellationToken);
        var current=await db.PasswordResetTokens.SingleAsync(x=>x.Id==entry.Id,cancellationToken);
        var now=DateTimeOffset.UtcNow;
        if(current.UsedAt is not null||current.ExpiresAt<=now)throw new ApiError("invalidResetToken");
        var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==current.UserId&&x.Enabled,cancellationToken)??throw new ApiError("invalidResetToken");
        var credentials=passwords.Hash(password);user.ChangePassword(credentials["hash"]!.ToString(),credentials["salt"]!.ToString());current.UsedAt=now;
        await db.Sessions.Where(x=>x.UserId==user.Id).ExecuteDeleteAsync(cancellationToken);
        await db.PasswordResetTokens.Where(x=>x.UserId==user.Id&&x.UsedAt==null).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.UsedAt,now),cancellationToken);
        await db.SaveChangesAsync(cancellationToken);await tx.CommitAsync(cancellationToken);
    }
}
