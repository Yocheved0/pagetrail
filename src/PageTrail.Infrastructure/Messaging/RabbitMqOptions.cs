using System.ComponentModel.DataAnnotations;
using RabbitMQ.Client;

namespace PageTrail.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string HostName { get; init; } = "";

    [Range(1, 65535)]
    public int Port { get; init; } = 5672;

    [Required]
    public string UserName { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";

    public ConnectionFactory CreateConnectionFactory() =>
        new() { HostName = HostName, Port = Port, UserName = UserName, Password = Password };
}
