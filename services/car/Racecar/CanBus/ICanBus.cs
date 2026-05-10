namespace Racecar.CanBus;

public interface ICanBus
{
    bool IsOpen { get; }

    bool IsSilentOnCanBus { get; set; }

    long MessagesRx { get; }

    long MessagesTx { get; }

    event Action<CanMessage> Received;

    Task<int> OpenAsync(string interfaceName, int speed);

    void Send(CanMessage message);

    Task<int> CloseAsync();
}