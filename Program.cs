using DotNetEnv;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Load the .env file
        Env.Load();

        // Add HttpClient
        services.AddHttpClient("UserRegistrationClient");

        // Add Worker Service
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();