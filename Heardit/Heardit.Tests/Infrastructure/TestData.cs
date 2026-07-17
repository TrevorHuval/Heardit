using Heardit.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Heardit.Tests.Infrastructure;

public static class TestData
{
    /// <summary>A minimally-populated user, ready to insert straight into the DbContext.</summary>
    public static HearditUser MakeUser(string userName, string? id = null)
    {
        id ??= Guid.NewGuid().ToString();
        return new HearditUser(userName)
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@example.com",
            NormalizedEmail = $"{userName}@example.com".ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
    }

    /// <summary>
    /// A substituted <see cref="UserManager{TUser}"/>. The services only call the lookup methods
    /// (<c>FindByIdAsync</c> / <c>FindByNameAsync</c>), which are virtual and get intercepted, so each
    /// test arranges just those. The real constructor tolerates null dependencies as long as the store
    /// is supplied, so we pass a substitute store and nulls for everything else.
    /// </summary>
    public static UserManager<HearditUser> SubstituteUserManager()
    {
        var store = Substitute.For<IUserStore<HearditUser>>();
        // UserManager's constructor accepts nulls for everything but the store; null! silences the
        // non-nullable-argument warnings without changing the (intentionally null) runtime values.
        return Substitute.For<UserManager<HearditUser>>(
            store, null!, null!, null!, null!, null!, null!, null!, null!);
    }
}
