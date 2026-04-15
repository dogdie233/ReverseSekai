using SekaiApiModel.CP.Realtime;

namespace SelfHostSekai.Services.Multiplayer;

public interface IDiarkisService
{
    /// <summary>
    /// Connects to the Diarkis server using TCP.
    /// </summary>
    Task<bool> ConnectAsync();

    /// <summary>
    /// Disconnects from the Diarkis server.
    /// </summary>
    Task<bool> DisconnectAsync();

    /// <summary>
    /// Checks if connected to Diarkis.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Sends a message to Diarkis.
    /// </summary>
    Task<bool> SendMessageAsync(byte[] messageData);

    /// <summary>
    /// Registers a message handler for a specific message type.
    /// </summary>
    void RegisterMessageHandler(string messageType, Func<byte[], Task> handler);

    /// <summary>
    /// Unregisters a message handler.
    /// </summary>
    void UnregisterMessageHandler(string messageType);
}
