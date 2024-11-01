using System.Security.Cryptography.X509Certificates;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

var x509_pem = @"C:\Users\pc\Documents\_Tmp\MQTTCertificate\openssl.crt.pem";  //Provide your client certificate .cer.pem file path
var x509_key = @"C:\Users\pc\Documents\_Tmp\MQTTCertificate\openssl.key.pem";  //Provide your client certificate .key.pem file path

var client1Topic = "client1/temperature/chambre";

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
    // Use builder classes where possible in this project.
    var mqttClientOptions = new MqttClientOptionsBuilder()
        .WithTcpServer("demomqtt.francecentral-1.ts.eventgrid.azure.net", 8883)
        //.WithProtocolVersion(MqttProtocolVersion.V500)
        .WithClientId("demo-mqtt")
        .WithCredentials("demo-mqtt", "")
        .WithTlsOptions(new MqttClientTlsOptionsBuilder()
                .WithSslProtocols(System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13)
                .WithClientCertificates(certificates) // Missed to update the client certificate provider details**
                .Build())
        //.WithKeepAlivePeriod(TimeSpan.FromSeconds(5 * 60))
        .WithWillPayload("Bye !")
        .Build();

    // This will throw an exception if the server is not available.
    // The result from this message returns additional data which was sent
    // from the server. Please refer to the MQTT protocol specification for details.
    var response = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

    Console.WriteLine("The MQTT client is connected.");
    Console.WriteLine("---------------------------------");
    Console.WriteLine("1 - Send multiple message (QoS 0)");
    Console.WriteLine("2 - Send multiple message (QoS 1)");
    Console.WriteLine("3 - Send a Request/Response message to the server");
    Console.WriteLine("X - Disconnect and send Last Will and Testament (LWT) message");

    Console.WriteLine("Your choice : ");
    var choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
            await SendMultipleMessage(mqttClient);
            break;
        case "2":
            await SendMultipleMessageQoS1(mqttClient);
            break;
        case "3":
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

async Task SendMultipleMessageQoS1(IMqttClient mqttClient)
{
    // loop for 5 messages with randome temperature values
    for (int i = 0; i < 5; i++)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(client1Topic)
                .WithPayload(new Random().Next(15, 25).ToString())
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

        await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

        Console.WriteLine($"Message {i + 1} sent");

        Thread.Sleep(1000);
    }
}

async Task SendRequestResponseMessage(IMqttClient mqttClient){
    var response = await mqttClient.SubscribeAsync("client1/temperature/chambre/response", MqttQualityOfServiceLevel.AtLeastOnce);
    var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic("client1/temperature/chambre/request")
                .WithPayload("Requesting temperature value")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithResponseTopic("client1/temperature/chambre/response")
                .Build();

    await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

    Console.WriteLine("Request message sent. Waiting for response...");

    Console.WriteLine("Response message received");
    Console.WriteLine("Payload : " + response.ToString());
}

async Task DisconnectAndSendLastWill(IMqttClient mqttClient)
{
    // Send a clean disconnect to the server by calling _DisconnectAsync_. Without this the TCP connection
    // gets dropped and the server will handle this as a non clean disconnect (see MQTT spec for details).
    var mqttClientDisconnectOptions = mqttFactory.CreateClientDisconnectOptionsBuilder().Build();

    await mqttClient.DisconnectAsync(mqttClientDisconnectOptions, CancellationToken.None);
}
