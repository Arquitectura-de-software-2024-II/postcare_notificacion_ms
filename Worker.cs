using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
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
        _factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_IP") ?? "localhost",
            Port = 5672,
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest", // Usuario de RabbitMQ
            Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest", // Contraseña de RabbitMQ
            AutomaticRecoveryEnabled = true, // Habilitar reconexión automática
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10) // Intentar reconectar cada 10s
        }; // RabbitMQ

        var baseAddress = Environment.GetEnvironmentVariable("API_GATEWAY")
                                ?? "http://localhost:8080"; // Default fallback;
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

            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(message);

                if (data != null && data.ContainsKey("idPaciente") && data.ContainsKey("prioridad"))
                {
                    string idPaciente = data["idPaciente"];
                    string prioridad = data["prioridad"];

                    if (prioridad == "prioridad 1" || prioridad == "prioridad 2")
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"/auth/emergency-contact/{idPaciente}/");
                        var response = await _httpClient.SendAsync(request, stoppingToken);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseBody = await response.Content.ReadAsStringAsync(stoppingToken);
                            var contacto = JsonSerializer.Deserialize<JsonElement>(responseBody);
                            string numeroTelefonico = contacto.GetProperty("telefono").GetString() ?? string.Empty;

                            if (!string.IsNullOrEmpty(numeroTelefonico))
                            {
                                _logger.LogInformation($"[x] Contacto obtenido: {numeroTelefonico}");

                                var smsSender = new SmsSender(_logger);
                                string mensaje = "¡Hola! Te informamos que recientemente se ha reportado un síntoma riesgoso tras la operación de tu familiar en el hospital de la Universidad Nacional de Colombia.";
                                await smsSender.SendSmsAsync(numeroTelefonico, mensaje);
                                _logger.LogInformation($"[x] Número telefónico enviado: {numeroTelefonico}");
                            }
                            else
                            {
                                _logger.LogWarning("[!] No se recibió un número de teléfono válido.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"[!] Error al obtener contacto. Código de respuesta: {response.StatusCode}");
                            return;
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[!] Error al realizar la petición HTTP: {ex.Message}");
                return;
            }

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
