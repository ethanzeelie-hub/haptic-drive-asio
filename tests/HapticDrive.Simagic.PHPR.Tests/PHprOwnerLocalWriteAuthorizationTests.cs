using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprOwnerLocalWriteAuthorizationTests
{
    [Fact]
    public void Current_StartsAuthorized()
    {
        var authorization = new PHprOwnerLocalWriteAuthorization();

        var current = authorization.Current;

        Assert.True(current.IsAuthorized);
        Assert.NotNull(current.AuthorizedAtUtc);
        Assert.Equal(1, current.Generation);
        Assert.Equal("Owner-local authorization active", current.Reason);
    }

    [Fact]
    public void Revoke_ThenTryAuthorize_RestoresAuthorizationWithoutPhrase()
    {
        var authorization = new PHprOwnerLocalWriteAuthorization();
        authorization.Revoke("interlock latched for test");

        var accepted = authorization.TryAuthorize(null);

        Assert.True(accepted);
        Assert.True(authorization.Current.IsAuthorized);
        Assert.NotNull(authorization.Current.AuthorizedAtUtc);
        Assert.Equal(3, authorization.Current.Generation);
        Assert.Equal("Owner-local authorization restored", authorization.Current.Reason);
    }
}
