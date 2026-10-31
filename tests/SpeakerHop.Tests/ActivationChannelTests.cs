using System.IO.Pipes;
using SpeakerHop.Services;

namespace SpeakerHop.Tests;

public sealed class ActivationChannelTests
{
    [Fact]
    public async Task RequestSettings_DeliversToExistingInstance()
    {
        var pipe = $"SpeakerHop.Tests.{Guid.NewGuid():N}";
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new ActivationChannel(pipe, () => received.TrySetResult());

        Assert.True(await ActivationChannel.RequestSettingsAsync(pipe, TimeSpan.FromSeconds(3)));
        await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task RequestSettings_WhenNoInstanceExists_ReturnsFalseWithinTimeout()
    {
        Assert.False(await ActivationChannel.RequestSettingsAsync(
            $"SpeakerHop.Tests.{Guid.NewGuid():N}", TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public async Task EmptyClient_DoesNotPreventNextRequest()
    {
        var pipe = $"SpeakerHop.Tests.{Guid.NewGuid():N}";
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new ActivationChannel(pipe, () => received.TrySetResult());
        using (var client = new NamedPipeClientStream(".", pipe, PipeDirection.Out, PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(3000);
        }

        Assert.True(await ActivationChannel.RequestSettingsAsync(pipe, TimeSpan.FromSeconds(3)));
        await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }
}
