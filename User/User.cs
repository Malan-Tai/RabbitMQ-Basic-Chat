using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;

class User
{
    static string username;
    static string roomname;

    // run username roomname
    static void Main(string[] args)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using(var connection = factory.CreateConnection())
        using(var download = connection.CreateModel())
        using(var upload = connection.CreateModel())
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: {0} username roomname",
                                        Environment.GetCommandLineArgs()[0]);
                Console.WriteLine(" Press [enter] to exit.");
                Console.ReadLine();
                Environment.ExitCode = 1;
                return;
            }
            username = args[0];
            roomname = args[1];

            // declare receiving queue via exchange
            download.ExchangeDeclare(exchange: "download",
                                    type: "direct");
            var queueName = download.QueueDeclare().QueueName;

            // bind with username and roomname
            download.QueueBind(queue: queueName,
                                exchange: "download",
                                routingKey: username);
            download.QueueBind(queue: queueName,
                                exchange: "download",
                                routingKey: roomname);         

            // consume from receiving queue
            var consumer = new EventingBasicConsumer(download);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var receivedMessage = Encoding.UTF8.GetString(body);
                Console.WriteLine(receivedMessage);
            };
            download.BasicConsume(queue: queueName,
                                autoAck: true,          // auto ack because no delay for printing
                                consumer: consumer);

            // declare sending queue
            upload.QueueDeclare(queue: "upload",
                                durable: false,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

            SendMessage("__connect_message", roomname, upload, "upload");

            string message;
            while ((message = Console.ReadLine()) != "/quit")
            {
                var split = message.Split(' ');

                switch (split[0])
                {
                    case "/whisper":
                        if (split.Length < 3) Console.WriteLine("Usage: /whisper targetName message");
                        else SendMessage(string.Join(' ', split.Skip(2).ToArray()), split[1], upload, "upload");
                        break;
                    case "/room":
                        if (split.Length != 2) Console.WriteLine("Usage: /room roomname");
                        else
                        {
                            SendMessage("__disconnect_message", roomname, upload, "upload");
                            roomname = split[1];
                            SendMessage("__connect_message", roomname, upload, "upload");
                        }
                        break;
                    default:
                        SendMessage(message, roomname, upload, "upload");
                        break;
                }
            }

            SendMessage("__disconnect_message", roomname, upload, "upload");
        }

        Console.WriteLine(" Press [enter] to exit.");
        Console.ReadLine();
    }

    private static void SendMessage(string message, string target, IModel channel, string channelName)
    {
        Message msgStruct = new Message
        {
            sender = username,
            target = target,
            message = message
        };

        var body = GetBytes(msgStruct);

        channel.BasicPublish(exchange: "",
                            routingKey: channelName,
                            basicProperties: null,
                            body: body);
    }

    private static byte[] GetBytes(Message message)
    {
        int size = Marshal.SizeOf(message);
        byte[] arr = new byte[size];

        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(message, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);
        return arr;
    }
}

