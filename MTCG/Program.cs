using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Npgsql;
using SWE.Models;
using System;
using System.Net.Sockets;
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

        server.Start("localhost", 10001);

        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            string connectionString = $"Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432";

            services.AddSingleton<NpgsqlConnection>(_ => new NpgsqlConnection(connectionString));

            services.AddSingleton<TcpServer>();
            services.AddSingleton<Router>();
            services.AddSingleton<UserService>(provider =>
            {
                var connectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432";
                var connection = new NpgsqlConnection(connectionString);
                return new UserService(connection);
            }); services.AddSingleton<Database>();
            services.AddSingleton<CardService>();
            services.AddSingleton<Package>();
            services.AddTransient<Card>();
        });

}