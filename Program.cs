using DotNetEnv;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Load the .env file
        Env.Load();

        // Add HttpClient with a base address from environment variables
        services.AddHttpClient("UserRegistrationClient", client =>
        {
            var apiGatewayUri = Environment.GetEnvironmentVariable("API_GATEWAY_URI") 
                                ?? "http://api_gateway";

            client.BaseAddress = new Uri(apiGatewayUri);
            client.DefaultRequestHeaders.Add("Content-Type", "application/json");
        });

        // Add Worker Service
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
