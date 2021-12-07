using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;


struct Message
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string sender;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string target;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 280)]
    public string message;
}

class Moderator
{
    static void Main(string[] args)
    {
        
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
                var messageStruct = fromBytes(body);

                var sender = messageStruct.sender;
                var target = messageStruct.target;
                var message = messageStruct.message;
                Console.WriteLine(" [x] Received {0} from {1} to {2}", message, sender, target);

                Thread.Sleep(delay * 1000);

                var sendMessage = Encoding.UTF8.GetBytes("[" + target + "] " + sender + " : " + message);
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

        Message fromBytes(byte[] arr) 
        {
            Message str = new Message();

            int size = Marshal.SizeOf(str);
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.Copy(arr, 0, ptr, size);

            str = (Message)Marshal.PtrToStructure(ptr, str.GetType());
            Marshal.FreeHGlobal(ptr);

            return str;
        }

    }
}
