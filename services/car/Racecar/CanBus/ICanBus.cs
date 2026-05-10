namespace Racecar.CanBus;

public interface ICanBus
{
    bool IsOpen { get; }

    bool IsSilentOnCanBus { get; set; }

    event Action<CanMessage> Received;

    Task<int> OpenAsync(int speed);

    Task SendAsync(CanMessage message);

    Task<int> CloseAsync();
}