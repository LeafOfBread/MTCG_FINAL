using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SWE.Models;
using System;
using System.Threading.Tasks;

public class Program
{
    private static readonly string host = "localhost";
    private static readonly string username = "postgres";
    private static readonly string password = "fhtw";
    private static readonly string database = "mtcg";

    private static readonly string connectionString = $"Host={host};Username={username};Password={password};Database={database};Port=5432";

    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        var server = host.Services.GetRequiredService<TcpServer>();

        // Start the server with the desired host and port
        server.Start("localhost", 10001);

        // The server should run indefinitely, so we won't finish Main until it's done.
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // Register TcpServer, Router, UserService, and Database
                services.AddSingleton<TcpServer>();
                services.AddSingleton<Router>();
                services.AddSingleton<UserService>();
                services.AddSingleton<Database>();
                services.AddSingleton<CardService>();
                services.AddSingleton<Package>();

                // Register the connection string and NpgsqlConnection for dependency injection
                services.AddSingleton<NpgsqlConnection>(provider =>
                    new NpgsqlConnection(connectionString));
            });
}
