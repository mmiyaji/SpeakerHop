using SpeakerHop.Services;

namespace SpeakerHop.Tests;

public sealed class SingleInstanceLeaseTests
{
    [Fact]
    public void TryAcquire_OnlyOneLeaseCanOwnAPathAtATime()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SpeakerHop.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "instance.lock");
        try
        {
            using var first = SingleInstanceLease.TryAcquire(path);
            Assert.NotNull(first);
            Assert.Null(SingleInstanceLease.TryAcquire(path));

            first.Dispose();
            using var reacquired = SingleInstanceLease.TryAcquire(path);
            Assert.NotNull(reacquired);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TryAcquire_IndependentPathsCanBeOwnedTogether()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SpeakerHop.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var first = SingleInstanceLease.TryAcquire(Path.Combine(directory, "first.lock"));
            using var second = SingleInstanceLease.TryAcquire(Path.Combine(directory, "second.lock"));

            Assert.NotNull(first);
            Assert.NotNull(second);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TryAcquire_DoesNotTreatUnrelatedIoFailureAsAnotherInstance()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SpeakerHop.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var blocker = Path.Combine(directory, "blocker");
        File.WriteAllText(blocker, "not a directory");
        try
        {
            Assert.Throws<IOException>(() => SingleInstanceLease.TryAcquire(Path.Combine(blocker, "instance.lock")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
