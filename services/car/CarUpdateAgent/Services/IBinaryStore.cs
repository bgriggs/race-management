namespace CarUpdateAgent.Services;

public interface IBinaryStore
{
    /// <summary>
    /// Path of the staging file where an incoming binary is written before swap.
    /// Pass this to <see cref="IHashVerifier"/> after saving.
    /// </summary>
    string IncomingPath { get; }

    /// <summary>Whether a backup binary exists and rollback is possible.</summary>
    bool HasBackup { get; }

    /// <summary>Write <paramref name="source"/> to the incoming staging path.</summary>
    Task SaveIncomingAsync(Stream source, CancellationToken cancellationToken);

    /// <summary>
    /// Back up the current active binary then atomically promote the incoming
    /// binary to active. Call only after hash verification has passed.
    /// </summary>
    void Swap();

    /// <summary>
    /// Restore the backup binary to the active path.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no backup exists.</exception>
    void Rollback();
}
