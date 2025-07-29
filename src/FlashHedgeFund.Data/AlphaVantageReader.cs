using FlashHedgeFund.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace FlashHedgeFund.Data;

/// <summary>
/// Alpha Vantage data reader with batch support and caching
/// </summary>
public class AlphaVantageReader : IDataReader
{
    private const string BaseUrl = "https://www.alphavantage.co/query";
    private const string Function = "TIME_SERIES_DAILY_ADJUSTED";
    private readonly HttpClient _http;
    private readonly ILogger<AlphaVantageReader> _logger;
    private readonly string _apiKey;
    private readonly string _dataDir;

    private static readonly ConcurrentDictionary<string, (DateTime Retrieved, StockContext Context)> _memoryCache = new();
    private readonly TimeSpan _fileTtl;

    public AlphaVantageReader(HttpClient http, ILogger<AlphaVantageReader> logger, IConfiguration config)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["AlphaVantage:ApiKey"] ?? throw new InvalidOperationException("AlphaVantage:ApiKey missing");
        _dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(_dataDir);

        _fileTtl = TimeSpan.TryParse(config["AlphaVantage:CacheTtl"], out var ttl) ? ttl : TimeSpan.FromHours(12);
    }

    public async Task<StockContext> GetStockContextAsync(string ticker, CancellationToken ct = default)
    {
        // 1. check in-memory cache
        if (_memoryCache.TryGetValue(ticker, out var entry) && DateTime.UtcNow - entry.Retrieved < _fileTtl)
            return entry.Context;

        var cacheFile = Path.Combine(_dataDir, $"{ticker.ToUpper()}.json");
        if (File.Exists(cacheFile) && File.GetLastWriteTimeUtc(cacheFile) > DateTime.UtcNow.Subtract(_fileTtl))
        {
            _logger.LogInformation("Loading {Ticker} from cache", ticker);
            await using var fs = File.OpenRead(cacheFile);
            using var sr = new StreamReader(fs);
            var json = await sr.ReadToEndAsync();
            var ctx = ParseJson(ticker, json);
            _memoryCache[ticker] = (DateTime.UtcNow, ctx);
            return ctx;
        }

        var url = $"{BaseUrl}?function={Function}&symbol={ticker}&apikey={_apiKey}&outputsize=compact";
        _logger.LogInformation("Requesting Alpha Vantage: {Url}", url);
        var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var content = await resp.Content.ReadAsStringAsync(ct);

        await File.WriteAllTextAsync(cacheFile, content, ct);
        var parsed = ParseJson(ticker, content);
        _memoryCache[ticker] = (DateTime.UtcNow, parsed);
        return parsed;
    }

    public async Task<IReadOnlyList<StockContext>> GetBatchStockContextAsync(IReadOnlyList<string> tickers, CancellationToken ct = default)
    {
        // Check if all tickers are in cache first
        var results = new List<StockContext>();
        var uncachedTickers = new List<string>();

        foreach (var ticker in tickers)
        {
            if (_memoryCache.TryGetValue(ticker, out var entry) && DateTime.UtcNow - entry.Retrieved < _fileTtl)
            {
                results.Add(entry.Context);
                continue;
            }

            var cacheFile = Path.Combine(_dataDir, $"{ticker.ToUpper()}.json");
            if (File.Exists(cacheFile) && File.GetLastWriteTimeUtc(cacheFile) > DateTime.UtcNow.Subtract(_fileTtl))
            {
                await using var fs = File.OpenRead(cacheFile);
                using var sr = new StreamReader(fs);
                var json = await sr.ReadToEndAsync();
                var ctx = ParseJson(ticker, json);
                _memoryCache[ticker] = (DateTime.UtcNow, ctx);
                results.Add(ctx);
            }
            else
            {
                uncachedTickers.Add(ticker);
            }
        }

        // Batch fetch uncached tickers
        if (uncachedTickers.Count > 0)
        {
            var batchResults = await FetchBatchQuotes(uncachedTickers, ct);
            results.AddRange(batchResults);
        }

        // Return in original order
        return tickers.Select(t => results.First(r => r.Ticker.Equals(t, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private async Task<List<StockContext>> FetchBatchQuotes(List<string> tickers, CancellationToken ct)
    {
        var symbols = string.Join(",", tickers);
        var url = $"{BaseUrl}?function=BATCH_STOCK_QUOTES&symbols={symbols}&apikey={_apiKey}";
        _logger.LogInformation("Batch requesting Alpha Vantage: {Count} symbols", tickers.Count);
        
        var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var content = await resp.Content.ReadAsStringAsync(ct);

        return ParseBatchJson(content, tickers);
    }

    private List<StockContext> ParseBatchJson(string json, List<string> tickers)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Stock Quotes", out var quotes))
            throw new InvalidOperationException("Unexpected batch quotes JSON");

        var results = new List<StockContext>();
        foreach (var quote in quotes.EnumerateArray())
        {
            var symbol = quote.GetProperty("1. symbol").GetString()!;
            var price = double.Parse(quote.GetProperty("2. price").GetString()!);
            
            var priceDict = new Dictionary<DateTime, double> { { DateTime.UtcNow.Date, price } };
            var ctx = new StockContext(symbol, DateTime.UtcNow, new Dictionary<string, double>(), priceDict);
            
            // Cache individual results
            var cacheFile = Path.Combine(_dataDir, $"{symbol.ToUpper()}.json");
            File.WriteAllText(cacheFile, JsonSerializer.Serialize(new { symbol, price, timestamp = DateTime.UtcNow }));
            _memoryCache[symbol] = (DateTime.UtcNow, ctx);
            
            results.Add(ctx);
        }

        return results;
    }

    private static StockContext ParseJson(string ticker, string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Time Series (Daily)", out var series))
            throw new InvalidOperationException("Unexpected AlphaVantage JSON");

        var priceDict = new Dictionary<DateTime, double>();
        foreach (var prop in series.EnumerateObject())
        {
            if (DateTime.TryParse(prop.Name, out var date))
            {
                var close = prop.Value.GetProperty("4. close").GetString();
                if (double.TryParse(close, out var price))
                    priceDict[date] = price;
            }
        }

        return new StockContext(ticker, DateTime.UtcNow, new Dictionary<string, double>(), priceDict);
    }
}
