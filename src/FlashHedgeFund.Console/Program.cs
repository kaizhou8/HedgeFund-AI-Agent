using FlashHedgeFund.Contracts;
using FlashHedgeFund.Data;
using FlashHedgeFund.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using Azure.AI.OpenAI;
using System.Diagnostics;

var root = new RootCommand("Flash HedgeFund AI – generate stock signals with multiple AI agents")
{
    new Option<string[]>("--agents", description: "Agent names", getDefaultValue: () => new[] { "warren_buffett", "cathie_wood" }),
    new Option<string[]>("--tickers", description: "Stock tickers") { IsRequired = true },
    new Option<int>("--max-parallel", description: "Max parallel requests", getDefaultValue: () => 4)
};

root.SetHandler(async (selectedAgents, tickers, maxParallel) =>
{
    using IHost host = Host.CreateDefaultBuilder()
        .ConfigureServices((ctx, services) =>
        {
            services.AddHttpClient();
            services.AddSingleton<IDataReader, AlphaVantageReader>();
            services.AddTransient<WarrenBuffettAgent>();
            services.AddTransient<CathieWoodAgent>();
            services.AddTransient<IAgent>(sp => sp.GetRequiredService<WarrenBuffettAgent>());
            services.AddTransient<IAgent>(sp => sp.GetRequiredService<CathieWoodAgent>());

            // Register OpenAI client
            var apiKey = ctx.Configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("OpenAI:ApiKey is missing in configuration.");
            services.AddSingleton(new OpenAIClient(apiKey));
        })
        .Build();

    var agents = host.Services.GetServices<IAgent>()
        .Where(a => selectedAgents.Contains(a.Name, StringComparer.OrdinalIgnoreCase))
        .ToArray();

    var reader = host.Services.GetRequiredService<IDataReader>();

    var semaphore = new SemaphoreSlim(maxParallel);

    // Use batch API for better performance
    var sw = Stopwatch.StartNew();
    var contexts = await reader.GetBatchStockContextAsync(tickers, CancellationToken.None);
    sw.Stop();
    Console.WriteLine($"Fetched {tickers.Length} stocks in {sw.ElapsedMilliseconds} ms (batch)");

    await Task.WhenAll(contexts.Select(async ctx =>
    {
        Console.WriteLine($"\n=== {ctx.Ticker.ToUpper()} ===");
        var ratingTasks = agents.Select(async a =>
        {
            await semaphore.WaitAsync();
            var agentSw = Stopwatch.StartNew();
            try
            {
                var rating = await a.EvaluateAsync(ctx);
                agentSw.Stop();
                Console.WriteLine($"[{a.Name}] completed in {agentSw.ElapsedMilliseconds} ms");
                return (a.Name, rating);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();
        
        var ratings = await Task.WhenAll(ratingTasks);
        foreach (var (name, rating) in ratings)
            Console.WriteLine($"[{name}] {rating.Recommendation} ({rating.Confidence:P0}) – {rating.Rationale}");
    }));
},
    root.Children.GetByAlias("--agents") as Option<string[]>,
    root.Children.GetByAlias("--tickers") as Option<string[]>,
    root.Children.GetByAlias("--max-parallel") as Option<int>);

return await root.InvokeAsync(args);
