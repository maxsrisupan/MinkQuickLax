using Microsoft.Extensions.Logging.Abstractions;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Update;

namespace MinkQuickLax.Platform.Tests;

public sealed class UpdateServiceTests
{
    /// <summary>build/pack.ps1 packs stable releases on "win" and pre-releases on "beta"; the app must ask for the same.</summary>
    [Theory]
    [InlineData(UpdateChannel.Stable, "win")]
    [InlineData(UpdateChannel.Beta, "beta")]
    public void Channels_MatchThePackScript(UpdateChannel channel, string expected)
    {
        Assert.Equal(expected, UpdateService.ChannelName(channel));
    }

    [Fact]
    public async Task ADevelopmentBuild_IsNotInstalled_AndNeverTouchesTheNetwork()
    {
        // The app calls this first thing in Main; without it Velopack has no idea where the app lives.
        Velopack.VelopackApp.Build().SetArgs([]).Run();
        using var updates = new UpdateService(NullLogger<UpdateService>.Instance);

        var result = await updates.CheckAndDownloadAsync(UpdateChannel.Stable, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateOutcome.NotInstalled, result.Outcome);
        Assert.Null(updates.ReadyVersion);
    }
}
