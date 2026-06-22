using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprSessionWriteAuthorizationTests
{
    [Fact]
    public void Current_StartsUnauthorized()
    {
        var authorization = new PHprSessionWriteAuthorization();

        var current = authorization.Current;

        Assert.False(current.IsAuthorized);
        Assert.Null(current.AuthorizedAtUtc);
        Assert.Equal(0, current.Generation);
        Assert.Equal("Not authorized", current.Reason);
    }

    [Fact]
    public void TryAuthorize_TrimsPhraseAndSetsAuthorizedSnapshot()
    {
        var authorization = new PHprSessionWriteAuthorization();

        var accepted = authorization.TryAuthorize($"  {PHprControlledWriteApproval.Phrase}  ");

        Assert.True(accepted);
        Assert.True(authorization.Current.IsAuthorized);
        Assert.NotNull(authorization.Current.AuthorizedAtUtc);
        Assert.Equal(1, authorization.Current.Generation);
        Assert.Equal("Authorized for this session", authorization.Current.Reason);
    }

    [Fact]
    public void TryAuthorize_FailedAttemptDoesNotChangeGeneration()
    {
        var authorization = new PHprSessionWriteAuthorization();

        var accepted = authorization.TryAuthorize("not the approval phrase");

        Assert.False(accepted);
        Assert.False(authorization.Current.IsAuthorized);
        Assert.Null(authorization.Current.AuthorizedAtUtc);
        Assert.Equal(0, authorization.Current.Generation);
        Assert.Equal("Not authorized", authorization.Current.Reason);
    }

    [Fact]
    public void Revoke_ClearsAuthorizationAndIncrementsGeneration()
    {
        var authorization = new PHprSessionWriteAuthorization();
        Assert.True(authorization.TryAuthorize(PHprControlledWriteApproval.Phrase));

        authorization.Revoke("  Test revoke reason.  ");

        Assert.False(authorization.Current.IsAuthorized);
        Assert.Null(authorization.Current.AuthorizedAtUtc);
        Assert.Equal(2, authorization.Current.Generation);
        Assert.Equal("Test revoke reason.", authorization.Current.Reason);
    }
}
