namespace SpeakerHop.Services;

/// <summary>
/// A package-identity-independent per-user process lease. Unlike named kernel objects,
/// this file remains shared when packaged and unpackaged builds run side by side.
/// </summary>
public sealed class SingleInstanceLease : IDisposable
{
    private readonly FileStream _stream;

    private SingleInstanceLease(FileStream stream) => _stream = stream;

    /// <summary>Claims the lease, or returns null when another process already owns it.</summary>
    public static SingleInstanceLease? TryAcquire(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path)
                ?? throw new ArgumentException("The lease path must include a directory.", nameof(path));
            Directory.CreateDirectory(directory);
            return new SingleInstanceLease(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException ex) when ((ex.HResult & 0xFFFF) is 32 or 33)
        {
            return null;
        }
    }

    public void Dispose() => _stream.Dispose();
}
