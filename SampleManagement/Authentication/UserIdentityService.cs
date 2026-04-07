using System.Runtime.InteropServices;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ENV=System.Environment;

namespace SampleManagement.Authentication;

public interface IUserIdentityService
{
    Task<ClaimsPrincipal> GetUserPrincipalAsync();
}

public class UserIdentityService(IDbContextFactory<FPSampleDbContext> dbFactory, IMemoryCache cache) : IUserIdentityService
{

    /// <summary>
    /// Gets the authentication state, which can be one of three things:
    /// Unauthenticated (associate number not in DB, HTTP 401 on attempted admin page access)
    /// Unauthorized (associate number in DB without admin privileges, HTTP 403 on attempted admin page access)
    /// Authorized (associate number in DB with admin privileges, successful navigation on admin page access)
    /// </summary>
    /// <returns>The authentication state of the current user</returns>
    public async Task<ClaimsPrincipal> GetUserPrincipalAsync()
    {
        // Get the username from the system (e.g. SUSU1057, SUSD5938)
        string associateString = ENV.UserName;
        string cacheKey = $"UserPrincipal_{associateString}";

        // If there's an identity stored in the cache, use that
        if (cache.TryGetValue(cacheKey, out ClaimsPrincipal? cachedPrincipal) && cachedPrincipal != null)
        {
            return cachedPrincipal;
        }

        // Check domain and name, verify that they match for SUS (not trivial to trick the environment variables)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) // 99% chance it goes here
        {
            string[] domainAndUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\');
            string domain = domainAndUser[0];
            associateString = domainAndUser[1];
            if (!(domain.Equals("STANLEYUS") && associateString.StartsWith("SUS")))
            {
                return new(new ClaimsIdentity());
            }
        }
        else // but fall back to environment variables
        {
            if (!(ENV.UserDomainName.Equals("STANLEYUS") && associateString.StartsWith("SUS")))
            {
                return new(new ClaimsIdentity());
            }
        }

        associateString = associateString[4..]; // trim the first four characters (SUSU, but also works for SUSD if an IT person wanted to peek)

        ClaimsPrincipal principal;
        // Extract associate number from remaining (hopefully all numeric) characters
        if (int.TryParse(associateString, out int associateNum))
        {
            using FPSampleDbContext context = dbFactory.CreateDbContext();
            Associate? associate = await context.Set<Associate>()
                .FirstOrDefaultAsync(a => a.AssociateNum == associateNum);

            if (associate != null)
            {
                // Create identifier, adding admin role if applicable
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, associate.Name ?? ""),
                    new("AssociateNum", associateNum.ToString())
                };

                if (associate.IsAdmin)
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                }

                ClaimsIdentity identity = new(claims, "AutoAuth");
                principal = new(identity);
            } else
            {
                principal = new(new ClaimsIdentity());
            }
        // If int.TryParse failed (prefix strip didn't get something that looked like associate number) or an associate with the parsed number wasn't found in the DB, return anonymous
        } else
        {
            principal = new(new ClaimsIdentity());
        }
        cache.Set(cacheKey, principal, TimeSpan.FromMinutes(10));

        return principal;
    }
}
