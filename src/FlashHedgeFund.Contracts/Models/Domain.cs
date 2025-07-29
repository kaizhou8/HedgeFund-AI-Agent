namespace FlashHedgeFund.Contracts;

/// <summary>
/// Investment recommendation enum
/// </summary>
public enum Recommendation
{
    Buy,
    Hold,
    Sell
}

/// <summary>
/// Stock rating result
/// </summary>
public record StockRating(
    Recommendation Recommendation,
    double Confidence,
    string Rationale,
    IReadOnlyDictionary<string, string> KeyMetrics);

/// <summary>
/// Stock context data
/// </summary>
public record StockContext(
    string Ticker,
    DateTime RetrievedAt,
    IReadOnlyDictionary<string, double> FundamentalData,
    IReadOnlyDictionary<DateTime, double> PriceSeries);

/// <summary>
/// AI investment agent interface
/// </summary>
public interface IAgent
{
    string Name { get; }
    Task<StockRating> EvaluateAsync(StockContext context, CancellationToken ct = default);
}

/// <summary>
/// Data reader interface
/// </summary>
public interface IDataReader
{
    Task<StockContext> GetStockContextAsync(string ticker, CancellationToken ct = default);
    Task<IReadOnlyList<StockContext>> GetBatchStockContextAsync(IReadOnlyList<string> tickers, CancellationToken ct = default);
}
