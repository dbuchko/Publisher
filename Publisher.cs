﻿using System;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Logging;
using Steeltoe.CloudFoundry.Connector.Rabbit;
using Steeltoe.Extensions.Configuration;

class Publisher
{
    private static string taskQueueName;

    public static void Main(string[] args)
    {
        RabbitMqConsoleEventListener loggingEventSource = new RabbitMqConsoleEventListener();

        int numMsgsPublished = 1;

        // Set default interval to publish messages
        ushort heartbeatInterval = 20;
        string heartbeatIntervalStr = Environment.GetEnvironmentVariable("HEARTBEAT_INTERVAL_SEC");
        if (heartbeatIntervalStr == null)
        {
            Console.WriteLine("HEARTBEAT_INTERVAL_SEC environment variable not defined, using default.");
        }
        else
        {
            heartbeatInterval = Convert.ToUInt16(heartbeatIntervalStr);
        }

        Console.WriteLine("Setting heartbeat interval to {0} s", heartbeatInterval);

        // Set default interval to publish messages
        int publishIntervalSec = 600;
        string publishIntervalStr = Environment.GetEnvironmentVariable("PUBLISH_INTERVAL_SEC");
        if (publishIntervalStr == null)
        {
            Console.WriteLine("PUBLISH_INTERVAL_SEC environment variable not defined, using default.");
        }
        else
        {
            publishIntervalSec = Convert.ToInt32(publishIntervalStr);
        }

        Console.WriteLine("Message publish interval is {0} s", publishIntervalSec);
        int publishInterval = publishIntervalSec * 1000;

        int port = 5672;
        string portStr = Environment.GetEnvironmentVariable("RABBITMQ_NODE_PORT");
        if (portStr != null)
        {
            port = Convert.ToInt32(portStr);
        }

        var factory = new ConnectionFactory() { HostName = "localhost", Port = port };
        if (Environment.GetEnvironmentVariable("VCAP_SERVICES") != null)
        {
            // Running on PCF
            IServiceCollection services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCloudFoundry()
                .Build();
            services.AddRabbitConnection(config);
            factory = services.BuildServiceProvider().GetService<ConnectionFactory>();
        }


        taskQueueName = Environment.GetEnvironmentVariable("QUEUE_NAME");
        if (taskQueueName == null)
        {
            taskQueueName = "task_queue";
        }
        Console.WriteLine("Setting queue name to {0}", taskQueueName);

        // No need to explicitly set this value, default is already true
        // factory.AutomaticRecoveryEnabled = true;

        // Since we have a durable queue anyways, there should be no need to recreate it on a connection failure.
        // Otherwise this currently can result in exceptions (if the durable queue home node is down), that
        // hangs the RMQ client.
        factory.TopologyRecoveryEnabled = false;

         // Reduce the heartbeat interval so that bad connections are detected sooner than the default of 60s
        factory.RequestedHeartbeat = heartbeatInterval;

        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.QueueDeclare(queue: taskQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

            var message = "Happy Birthday!!";
            var body = Encoding.UTF8.GetBytes(message);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            while (true)
            {
                channel.BasicPublish(exchange: "", routingKey: taskQueueName, basicProperties: properties, body: body);
                Console.WriteLine("Published {0} messages", numMsgsPublished++);
                Thread.Sleep(publishInterval);
            }
        }

    }

}
