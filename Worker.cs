using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ConnectionFactory _factory;
    private readonly HttpClient _httpClient;

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _factory = new ConnectionFactory { HostName = "localhost" }; // RabbitMQ settings

        var baseAddress = "http://localhost:8080";
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(baseAddress); // Dirección del API Gateway (Nginx)
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

        // Crear conexión y canal de RabbitMQ
        using var connection = await _factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: "hello", durable: false, exclusive: false, autoDelete: false, arguments: null);
        _logger.LogInformation(" [*] Waiting for messages.");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            _logger.LogInformation($"[x] Received message: {message}");

            // Número de documento que viene en el mensaje
            string numeroDocumento = message;

            // Peticion para traer los contactos de emergencia
            // try
            // {
            //     var request = new HttpRequestMessage(HttpMethod.Get, $"/auth/users/{numeroDocumento}/contacts"); *****MODIFICAR POR ENDPOINT REAL*****
            //     var response = await _httpClient.SendAsync(request, stoppingToken);

            //     if (response.IsSuccessStatusCode)
            //     {
            //         var responseBody = await response.Content.ReadAsStringAsync(stoppingToken);

            //         // Deserializar los números telefónicos con manejo de null
            //         var numerosTelefonicos = JsonSerializer.Deserialize<string[]>(responseBody)
            //                             ?? Array.Empty<string>();

            //         _logger.LogInformation($"[x] Contactos obtenidos: {string.Join(", ", numerosTelefonicos)}");
            //     }
            //     else
            //     {
            //         _logger.LogWarning($"[!] Error al obtener contactos. Código de respuesta: {response.StatusCode}");
            //         return;
            //     }
            // }
            // catch (Exception ex)
            // {
            //     _logger.LogError($"[!] Error al realizar la petición HTTP: {ex.Message}");
            //     return;
            // }

            // ----Provisional mientras se crea el endpoint de traer contactos de emergencia----
            string[] numerosTelefonicos = ["+573142435418"];
            // ---------------------------------------------------------------------------------


            // Enviar SMS a los números obtenidos
            var smsSender = new SmsSender(_logger);
            string mensaje = $"¡Hola! Te informamos que el paciente con identificacion {numeroDocumento} ha reportado un sintoma tras su operacion en el hospital de la Universidad Nacional de Colombia.";
            await smsSender.SendSmsAsync(numerosTelefonicos, mensaje);

            _logger.LogInformation($"[x] Números telefónicos enviados: {string.Join(", ", numerosTelefonicos)}");

            // Simular trabajo adicional
            await Task.Delay(1000, stoppingToken);
        };

        await channel.BasicConsumeAsync("hello", autoAck: true, consumer: consumer);
        // Mantener el worker en ejecución
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker stopping at: {time}", DateTimeOffset.Now);
        return base.StopAsync(cancellationToken);
    }
};
