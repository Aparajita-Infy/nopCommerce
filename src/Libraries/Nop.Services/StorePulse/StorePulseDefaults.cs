using Nop.Core.Caching;

namespace Nop.Services.StorePulse;

/// <summary>
/// Fixed thresholds for the Store Pulse tiles. v1 has no settings class.
/// </summary>
public static partial class StorePulseDefaults
{
    /// <summary>
    /// Gets how many rows each tile returns. The tile total can be larger.
    /// </summary>
    public const int TopRows = 5;

    /// <summary>
    /// Gets the cache time, in minutes, for one tile.
    /// </summary>
    /// <remarks>
    /// <see cref="CacheKey.CacheTime"/> is minutes. <c>PrepareKeyForDefaultCache</c> overwrites whatever
    /// is set here, so the service assigns this value again after preparing the key.
    /// </remarks>
    public const int TileCacheTime = 5;

    /// <summary>
    /// Gets how long a shopping cart stays idle before it counts as abandoned.
    /// </summary>
    public static TimeSpan CartIdle => TimeSpan.FromHours(24);

    /// <summary>
    /// Gets the oldest cart activity that still counts as abandoned.
    /// </summary>
    public static TimeSpan CartMaxAge => TimeSpan.FromDays(30);

    /// <summary>
    /// Gets how old a pending payment must be before it counts.
    /// </summary>
    public static TimeSpan PendingAge => TimeSpan.FromHours(24);

    /// <summary>
    /// Gets how far back zero-result searches are counted.
    /// </summary>
    public static TimeSpan SearchWindow => TimeSpan.FromDays(7);

    /// <summary>
    /// Gets the cache-key name of the abandoned-cart tile.
    /// </summary>
    public static string AbandonedCartsTile => "abandoned-carts";

    /// <summary>
    /// Gets the cache-key name of the low-stock tile.
    /// </summary>
    public static string LowStockTile => "low-stock";

    /// <summary>
    /// Gets the cache-key name of the pending-payment tile.
    /// </summary>
    public static string PendingPaymentsTile => "pending-payments";

    /// <summary>
    /// Gets the cache-key name of the zero-result search tile.
    /// </summary>
    public static string ZeroResultSearchesTile => "zero-result-searches";

    /// <summary>
    /// Gets a key for caching one tile.
    /// </summary>
    /// <remarks>
    /// {0} : tile name
    /// {1} : store identifier. 0 is the all-stores scope, and the only key used for low stock.
    /// </remarks>
    public static CacheKey TileCacheKey => new("Nop.storepulse.{0}-{1}")
    {
        CacheTime = TileCacheTime
    };
}
