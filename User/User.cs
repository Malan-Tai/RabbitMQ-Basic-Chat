using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;

struct SentMessage
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string sender;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string target;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 280)]
    public string message;

    public bool privateMsg;
}

struct ReceivedMessage
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 400)]
    public string message;

    public ConsoleColor color;
}

class User
{
    static string username;
    static string roomname;

    // run username roomname
    static void Main(string[] args)
    {
        Console.Clear();

        var factory = new ConnectionFactory() { HostName = "localhost" };
        using(var connection = factory.CreateConnection())
        using(var download = connection.CreateModel())
        using(var upload = connection.CreateModel())
        {
            if (args.Length != 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("Usage: {0} username roomname",
                                        Environment.GetCommandLineArgs()[0]);
                Console.WriteLine(" Press [enter] to exit.");
                Console.ForegroundColor = ConsoleColor.Gray;
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
                var messageStruct = FromBytes(body);

                Console.ForegroundColor = messageStruct.color;
                Console.WriteLine(messageStruct.message);
                Console.ForegroundColor = ConsoleColor.Gray;
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

            SendMessage("__connect_message", roomname, false, upload, "upload");

            string message = "";
            while ((message = Console.ReadLine()) != "/quit")
            {
                var split = message.Split(' ');

                switch (split[0])
                {
                    case "/whisper":
                        if (split.Length < 3)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Usage: /whisper targetName message");
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                        else SendMessage(string.Join(' ', split.Skip(2).ToArray()), split[1], true, upload, "upload");
                        break;
                    case "/room":
                        if (split.Length != 2)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Usage: /room roomname");
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                        else
                        {
                            download.QueueUnbind(queueName, "download", roomname);
                            SendMessage("__disconnect_message", roomname, false, upload, "upload");
                            roomname = split[1];
                            SendMessage("__connect_message", roomname, false, upload, "upload");
                            download.QueueBind(queueName, "download", roomname);
                        }
                        break;
                    default:
                        SendMessage(message, roomname, false, upload, "upload");
                        break;
                }
            }

            SendMessage("__disconnect_message", roomname, false, upload, "upload");
        }

        Console.WriteLine(" Press [enter] to exit.");
        Console.ReadLine();
    }

    private static void SendMessage(string message, string target, bool privateMsg, IModel channel, string channelName)
    {
        SentMessage msgStruct = new SentMessage
        {
            sender = username,
            target = target,
            message = message,
            privateMsg = privateMsg
        };

        var body = GetBytes(msgStruct);

        channel.BasicPublish(exchange: "",
                            routingKey: channelName,
                            basicProperties: null,
                            body: body);
    }

    private static byte[] GetBytes(SentMessage message)
    {
        int size = Marshal.SizeOf(message);
        byte[] arr = new byte[size];

        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(message, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);
        return arr;
    }

    private static ReceivedMessage FromBytes(byte[] arr)
    {
        ReceivedMessage str = new ReceivedMessage();

        int size = Marshal.SizeOf(str);
        IntPtr ptr = Marshal.AllocHGlobal(size);

        Marshal.Copy(arr, 0, ptr, size);

        str = (ReceivedMessage)Marshal.PtrToStructure(ptr, str.GetType());
        Marshal.FreeHGlobal(ptr);

        return str;
    }
}

