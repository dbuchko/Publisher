using System;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Steeltoe.CloudFoundry.Connector.Rabbit;
using Steeltoe.Extensions.Configuration;

class Publisher
{
    public static void Main(string[] args)
    {
        int numMsgsPublished = 1;

        // Set default interval to publish messages
        int publishInterval = 600000;
        string publishIntervalStr = Environment.GetEnvironmentVariable("PUBLISH_INTERVAL_SEC");
        if (publishIntervalStr == null)
        {
            Console.WriteLine("PUBLISH_INTERVAL_SEC environment variable not defined, using default.");
        }
        else
        {
            publishInterval = Convert.ToInt32(publishIntervalStr) * 1000;
        }

        Console.WriteLine("Message publish interval is {0} ms", publishInterval);

        IServiceCollection services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCloudFoundry()
            .Build();
        services.AddRabbitConnection(config);
        var factory = services.BuildServiceProvider().GetService<ConnectionFactory>();

        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.QueueDeclare(queue: "task_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

            var message = "Happy Birthday!!";
            var body = Encoding.UTF8.GetBytes(message);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            while (true)
            {
                channel.BasicPublish(exchange: "", routingKey: "task_queue", basicProperties: properties, body: body);
                Console.WriteLine("Published {0} messages", numMsgsPublished++);
                Thread.Sleep(publishInterval);
            }
        }

    }

}