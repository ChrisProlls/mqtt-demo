﻿using System.Security.Cryptography.X509Certificates;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;

string x509_pem = @"C:\Users\pc\Documents\_Tmp\MQTTCertificate\openssl.crt.pem";  //Provide your client certificate .cer.pem file path
string x509_key = @"C:\Users\pc\Documents\_Tmp\MQTTCertificate\openssl.key.pem";  //Provide your client certificate .key.pem file path

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
        .Build();

    // This will throw an exception if the server is not available.
    // The result from this message returns additional data which was sent
    // from the server. Please refer to the MQTT protocol specification for details.
    var response = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

    Console.WriteLine("The MQTT client is connected.");


    Console.ReadLine();

    // Send a clean disconnect to the server by calling _DisconnectAsync_. Without this the TCP connection
    // gets dropped and the server will handle this as a non clean disconnect (see MQTT spec for details).
    var mqttClientDisconnectOptions = mqttFactory.CreateClientDisconnectOptionsBuilder().Build();

    await mqttClient.DisconnectAsync(mqttClientDisconnectOptions, CancellationToken.None);
}
