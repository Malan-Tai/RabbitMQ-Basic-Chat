using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;


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

class Moderator
{
    static void Main(string[] args)
    {
        Console.Clear();

        // moderator delay
        int delay = 0;
        if (args.Length > 0)
        {
            delay = Int32.Parse(args[0]);
        }
        
        
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using(var connection = factory.CreateConnection())
        using(var uploadChannel = connection.CreateModel())
        using(var downloadChannel = connection.CreateModel())
        {
            //setup emission
            downloadChannel.ExchangeDeclare(exchange: "download",
                                            type: "direct");


            //setup reception
            uploadChannel.QueueDeclare(queue: "upload",
                                durable: false,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

            uploadChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            Console.WriteLine(" [*] Waiting for messages.");

            var consumer = new EventingBasicConsumer(uploadChannel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var messageStruct = FromBytes(body);

                var sender = messageStruct.sender;
                var target = messageStruct.target;
                var message = messageStruct.message;
                Console.WriteLine(" [x] Received {0} from {1} to {2}", message, sender, target);

                Thread.Sleep(delay * 1000);

                ReceivedMessage response = new ReceivedMessage();

                if (messageStruct.privateMsg)
                {
                    response.message = sender + " whispers : " + message;
                    response.color = ConsoleColor.DarkCyan;
                }
                else if (messageStruct.message == "__connect_message")
                {
                    response.message = "** " + sender + " connected to room **";
                    response.color = ConsoleColor.DarkGray;
                }
                else if (messageStruct.message == "__disconnect_message")
                {
                    response.message = "** " + sender + " disconnected from room **";
                    response.color = ConsoleColor.DarkGray;
                }
                else
                {
                    response.message = "[" + target + "] " + sender + " : " + message;
                    response.color = ConsoleColor.Gray;
                }

                var sendMessage = GetBytes(response);

                downloadChannel.BasicPublish(exchange: "download",
                                            routingKey: target,
                                            basicProperties: null,
                                            body: sendMessage);

                Console.WriteLine(" [x] Done");

                // Note: it is possible to access the uploadChannel via
                //       ((EventingBasicConsumer)sender).Model here
                uploadChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            };
            uploadChannel.BasicConsume(queue: "upload",
                                autoAck: false,
                                consumer: consumer);

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }

    private static byte[] GetBytes(ReceivedMessage message)
    {
        int size = Marshal.SizeOf(message);
        byte[] arr = new byte[size];

        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(message, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);
        return arr;
    }

    private static SentMessage FromBytes(byte[] arr)
    {
        SentMessage str = new SentMessage();

        int size = Marshal.SizeOf(str);
        IntPtr ptr = Marshal.AllocHGlobal(size);

        Marshal.Copy(arr, 0, ptr, size);

        str = (SentMessage)Marshal.PtrToStructure(ptr, str.GetType());
        Marshal.FreeHGlobal(ptr);

        return str;
    }
}
