namespace Nop.Services.StorePulse;

/// <summary>
/// Store Pulse tiles for the admin dashboard. Each method returns a total and the top rows.
/// Permission checks stay in the admin factory.
/// </summary>
public partial interface IStorePulseService
{
    /// <summary>
    /// Gets abandoned shopping carts for registered customers.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 includes every store</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the total and the most recently updated carts.
    /// </returns>
    Task<PulseTile<AbandonedCartRow>> GetAbandonedCartsAsync(int storeId);

    /// <summary>
    /// Gets products and combinations at or below their minimum stock. Not filtered by store.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the combined total and the first rows of each low-stock query.
    /// </returns>
    Task<PulseTile<LowStockRow>> GetLowStockAsync();

    /// <summary>
    /// Gets orders whose payment is still pending and that were created before the pending-age threshold.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 includes every store</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the total and the oldest matching orders.
    /// </returns>
    Task<PulseTile<PendingPaymentRow>> GetPendingPaymentsAsync(int storeId);

    /// <summary>
    /// Gets keywords that returned no products inside the search window.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 includes every store. Keywords are grouped across the selected stores.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the number of distinct keywords and the most frequent ones.
    /// </returns>
    Task<PulseTile<ZeroResultSearchRow>> GetZeroResultSearchesAsync(int storeId);
}
