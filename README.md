# Flash HedgeFund AI

**Flash HedgeFund AI** is a .NET algorithmic program that provides trading signals by analyzing stocks using multiple AI agents. Each agent applies a different investment philosophy to decide whether a stock is a **buy**, **hold**, or **sell**. Agents also provide their **reasoning**, **confidence score**, **key metrics**, and **specific rules** behind each decision.

## Features

- **Multiple Investment Styles**: Warren Buffett (value investing), Cathie Wood (growth/innovation)
- **AI-Powered Analysis**: Each agent uses OpenAI GPT for sophisticated reasoning
- **Batch Processing**: Optimized API calls for multiple stocks simultaneously
- **Smart Caching**: Memory + disk caching with configurable TTL
- **Performance Monitoring**: Real-time timing statistics and concurrency control
- **Extensible Architecture**: Easy to add new agents and data providers

## Currently Implemented Agents

- `warren_buffett` - Value investing, competitive moats, long-term focus
- `cathie_wood` - Innovation potential, disruptive technology, growth momentum

Each agent integrates with an LLM (Large Language Model) trained for financial reasoning to generate insights behind its signals.

## Quick Start

### Prerequisites

- .NET 8.0 SDK
- OpenAI API key
- Alpha Vantage API key (free tier available)

### Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd FlashHedgeFundAI
```

2. Configure API keys in `src/FlashHedgeFund.Console/appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key"
  },
  "AlphaVantage": {
    "ApiKey": "your-alphavantage-api-key",
    "CacheTtl": "12:00:00"
  }
}
```

3. Build the solution:
```bash
dotnet build
```

4. Run analysis:
```bash
dotnet run --project src/FlashHedgeFund.Console -- --tickers AAPL MSFT --agents warren_buffett cathie_wood
```

### Command Line Options

```bash
--tickers       Stock tickers to analyze (required)
--agents        Agent names to use (default: warren_buffett, cathie_wood)
--max-parallel  Maximum parallel requests (default: 4)
```

### Example Output

```
Fetched 2 stocks in 1250 ms (batch)

=== AAPL ===
[warren_buffett] completed in 2340 ms
[warren_buffett] Buy (85%) – Strong competitive moat with ecosystem lock-in...
[cathie_wood] completed in 1890 ms
[cathie_wood] Hold (70%) – Solid innovation but valuation concerns...

=== MSFT ===
[warren_buffett] completed in 2100 ms
[warren_buffett] Buy (90%) – Excellent management and cloud dominance...
[cathie_wood] completed in 1950 ms
[cathie_wood] Buy (80%) – AI leadership and enterprise transformation...
```

## Architecture

### Project Structure

```
FlashHedgeFundAI/
├── src/
│   ├── FlashHedgeFund.Contracts/    # Domain models & interfaces
│   ├── FlashHedgeFund.Data/         # Data access & caching
│   ├── FlashHedgeFund.Agents/       # AI investment agents
│   └── FlashHedgeFund.Console/      # CLI application
└── tests/
    └── FlashHedgeFund.Tests/        # Unit tests
```

### Core Interfaces

```csharp
public interface IAgent
{
    string Name { get; }
    Task<StockRating> EvaluateAsync(StockContext context, CancellationToken ct = default);
}

public interface IDataReader
{
    Task<StockContext> GetStockContextAsync(string ticker, CancellationToken ct = default);
    Task<IReadOnlyList<StockContext>> GetBatchStockContextAsync(IReadOnlyList<string> tickers, CancellationToken ct = default);
}
```

## Performance Optimizations

- **Batch API Calls**: Alpha Vantage batch quotes for multiple stocks
- **Parallel Processing**: Concurrent agent evaluations and stock fetching
- **Multi-Level Caching**: In-memory + disk caching with configurable TTL
- **Concurrency Control**: Configurable parallel request limits
- **Performance Monitoring**: Real-time timing statistics

## Configuration

### Cache Settings

```json
{
  "AlphaVantage": {
    "CacheTtl": "12:00:00"  // Cache time-to-live (12 hours)
  }
}
```

### Environment Variables

Alternatively, use environment variables:
- `OpenAI__ApiKey`
- `AlphaVantage__ApiKey`
- `AlphaVantage__CacheTtl`

## Testing

Run unit tests:
```bash
dotnet test tests/FlashHedgeFund.Tests
```

The test suite includes:
- Mocked HTTP clients for Alpha Vantage API
- Mocked OpenAI responses for agent testing
- Integration tests for the complete pipeline

## Extending the System

### Adding a New Agent

1. Create a new class implementing `IAgent`:
```csharp
public class BenjaminGrahamAgent : IAgent
{
    public string Name => "benjamin_graham";
    
    public async Task<StockRating> EvaluateAsync(StockContext context, CancellationToken ct = default)
    {
        // Implement deep value analysis logic
    }
}
```

2. Register in DI container (`Program.cs`):
```csharp
services.AddTransient<BenjaminGrahamAgent>();
services.AddTransient<IAgent>(sp => sp.GetRequiredService<BenjaminGrahamAgent>());
```

### Adding a New Data Provider

1. Implement `IDataReader` interface
2. Register in DI container
3. Configure API keys and settings

## API Keys Setup

### OpenAI API Key
1. Visit [OpenAI Platform](https://platform.openai.com/account/api-keys)
2. Create a new API key
3. Add to configuration

### Alpha Vantage API Key
1. Visit [Alpha Vantage](https://www.alphavantage.co/support/#api-key)
2. Get your free API key
3. Add to configuration

## Cache Management

Data is cached automatically:
- **Memory Cache**: Fast access for recent requests
- **Disk Cache**: Persistent storage in `data/` directory
- **TTL Control**: Configurable cache expiration

To force refresh, delete the `data/` folder or adjust TTL settings.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Disclaimer

This software is for educational and research purposes only. It is not financial advice. Always consult with qualified financial professionals before making investment decisions. Past performance does not guarantee future results.
