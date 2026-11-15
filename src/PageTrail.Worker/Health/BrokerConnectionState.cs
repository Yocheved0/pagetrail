using RabbitMQ.Client;

namespace PageTrail.Worker.Health;

public sealed class BrokerConnectionState
{
    private IConnection? _connection;

    public bool IsOpen => Volatile.Read(ref _connection)?.IsOpen == true;

    public void Track(IConnection connection) => Volatile.Write(ref _connection, connection);
}
