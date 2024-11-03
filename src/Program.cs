using System.Security.Cryptography.X509Certificates;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

const string x509_pem = @"C:\Users\pc\Documents\_Tmp\MQTTCertificate\openssl.crt.pem";  //Provide your client certificate .cer.pem file path
const string x509_key = @"C:\Users\pc\Documents\_Tmp\MQTTCertificate\openssl.key.pem";  //Provide your client certificate .key.pem file path

const string client1Topic = "client1/temperature/chambre";

const string clientSender = "demo-mqtt";
const string clientSubscriber = "demo-mqtt2";

// Load certificate and private key from PEM files
var certificate = new X509Certificate2(X509Certificate2.CreateFromPemFile(x509_pem, x509_key).Export(X509ContentType.Pkcs12));

// Add the loaded certificate to a certificate collection
X509Certificate2Collection certificates = new X509Certificate2Collection
{
    certificate
};

Console.WriteLine("Getting certificate");

var mqttFactory = new MqttFactory();

using (var mqttClient = mqttFactory.CreateMqttClient())
{
    Console.WriteLine("Who are you ?");
    Console.WriteLine("1 - Client 1 (sender)");
    Console.WriteLine("2 - Client 2 (subscriber)");

    Console.WriteLine("Your choice : ");
    var client = IsSenderClient() ? clientSender : clientSubscriber;

    // Use builder classes where possible in this project.
    var mqttClientOptions = new MqttClientOptionsBuilder()
        .WithTcpServer("demomqtt.francecentral-1.ts.eventgrid.azure.net", 8883)
        .WithProtocolVersion(MqttProtocolVersion.V500)
        .WithClientId(client)
        .WithCredentials(client, "")
        .WithTlsOptions(new MqttClientTlsOptionsBuilder()
                .WithSslProtocols(System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13)
                .WithClientCertificates(certificates)
                .Build())
        .WithWillPayload("Bye !")
        .WithWillTopic($"{client1Topic}/lwt")
        .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();

    var response = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

    Console.WriteLine("The MQTT client is connected.");
    Console.WriteLine("---------------------------------");

    if (IsSubscriberClient(client))
        await SubscribeToTopics(client1Topic, mqttClient);

    while (true)
        await Menu(mqttClient);
}

async Task Menu(IMqttClient mqttClient)
{
    Console.Clear();
    Console.WriteLine("1 - Send multiple message (QoS 0)");
    Console.WriteLine("2 - Send a Request/Response message to the server");
    Console.WriteLine("X - Disconnect and send Last Will and Testament (LWT) message");

    Console.WriteLine("Your choice : ");
    var choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
            await SendMultipleMessage(mqttClient);
            break;
        case "2":
            await SendRequestResponseMessage(mqttClient);
            break;
        case "X":
            await DisconnectAndSendLastWill(mqttClient);
            break;
        default:
            Console.WriteLine("Invalid choice.");
            break;
    }
}

async Task SendMultipleMessage(IMqttClient mqttClient) {
    
    // loop for 5 messages with randome temperature values
    for (int i = 0; i < 5; i++)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(client1Topic)
                .WithPayload(new Random().Next(15, 25).ToString())
                .Build();
    
        await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

        Console.WriteLine($"Message {i + 1} sent");
        
        Thread.Sleep(1000);
    }
}

async Task SendRequestResponseMessage(IMqttClient mqttClient){
    var response = await mqttClient.SubscribeAsync($"{client1Topic}/response", MqttQualityOfServiceLevel.AtLeastOnce);

    var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic($"{client1Topic}/request")
                .WithPayload("Requesting temperature value")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithResponseTopic($"{client1Topic}/response")
                .Build();

    await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

    mqttClient.ApplicationMessageReceivedAsync += async e =>
    {
        Console.WriteLine("Received message from " + e.ApplicationMessage.Topic);
        Console.WriteLine("Payload : " + Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
    };

    Console.WriteLine("Request message sent. Waiting for response...");

    Console.WriteLine("Response message received");
    Console.WriteLine("Payload : " + response.ToString());
}

async Task DisconnectAndSendLastWill(IMqttClient mqttClient)
{
    // Send a clean disconnect to the server by calling _DisconnectAsync_. Without this the TCP connection
    // gets dropped and the server will handle this as a non clean disconnect (see MQTT spec for details).
    var mqttClientDisconnectOptions = mqttFactory
        .CreateClientDisconnectOptionsBuilder()
        .WithReason(MqttClientDisconnectOptionsReason.DisconnectWithWillMessage)
        .Build();

    await mqttClient.DisconnectAsync(mqttClientDisconnectOptions, CancellationToken.None);
    Environment.Exit(0);
}

static bool IsSubscriberClient(string client)
{
    return client == clientSubscriber;
}

static bool IsSenderClient()
{
    return Console.ReadLine() == "1";
}

static async Task SubscribeToTopics(string client1Topic, IMqttClient mqttClient)
{
    await mqttClient.SubscribeAsync(client1Topic, MqttQualityOfServiceLevel.AtLeastOnce);
    await mqttClient.SubscribeAsync($"{client1Topic}/request", MqttQualityOfServiceLevel.AtLeastOnce);
    await mqttClient.SubscribeAsync($"{client1Topic}/lwt", MqttQualityOfServiceLevel.AtLeastOnce);

    Console.WriteLine("Subscribed to " + client1Topic);

    mqttClient.ApplicationMessageReceivedAsync += async e =>
    {
        Console.WriteLine("Received message from " + e.ApplicationMessage.Topic);
        Console.WriteLine("Payload : " + Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));

        if (e.ApplicationMessage.Topic == $"{client1Topic}/request")
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(e.ApplicationMessage.ResponseTopic)
                .WithPayload(new Random().Next(15, 25).ToString())
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

            Console.WriteLine("Response message sent");
        }
    };

    Console.WriteLine("Waiting for messages...");
    Console.ReadLine();

    Environment.Exit(0);
}