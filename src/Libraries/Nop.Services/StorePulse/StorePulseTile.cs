using Nop.Core.Domain.Catalog;

namespace Nop.Services.StorePulse;

/// <summary>
/// One Store Pulse tile: the full count, and at most <see cref="StorePulseDefaults.TopRows"/> rows.
/// </summary>
/// <typeparam name="T">Row type</typeparam>
public partial class PulseTile<T>
{
    /// <summary>
    /// Ctor
    /// </summary>
    public PulseTile()
    {
        TopRows = new List<T>();
    }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="totalCount">Full number of matching rows, not just <paramref name="topRows"/></param>
    /// <param name="topRows">Highest ranked rows</param>
    public PulseTile(int totalCount, IList<T> topRows)
    {
        TotalCount = totalCount;
        TopRows = topRows ?? new List<T>();
    }

    /// <summary>
    /// Gets or sets the full number of matching rows
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the highest ranked rows
    /// </summary>
    public IList<T> TopRows { get; set; }
}

/// <summary>
/// A registered customer's shopping cart whose latest item is inside the abandoned window.
/// </summary>
public partial class AbandonedCartRow
{
    /// <summary>
    /// Ctor
    /// </summary>
    public AbandonedCartRow()
    {
    }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="email">Customer email</param>
    /// <param name="itemCount">Sum of shopping-cart quantities for this customer in the store scope</param>
    /// <param name="lastUpdatedUtc">Latest shopping-cart item update (UTC)</param>
    public AbandonedCartRow(int customerId, string email, int itemCount, DateTime lastUpdatedUtc)
    {
        CustomerId = customerId;
        Email = email;
        ItemCount = itemCount;
        LastUpdatedUtc = lastUpdatedUtc;
    }

    /// <summary>
    /// Gets or sets the customer identifier
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer email
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the sum of shopping-cart item quantities
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Gets or sets the latest shopping-cart item update (UTC)
    /// </summary>
    public DateTime LastUpdatedUtc { get; set; }
}

/// <summary>
/// A product, or one attribute combination of a product, that is at or below its minimum stock.
/// </summary>
public partial class LowStockRow
{
    /// <summary>
    /// Ctor
    /// </summary>
    public LowStockRow()
    {
    }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="combinationId">Combination identifier; null for a product row</param>
    /// <param name="name">Product name</param>
    /// <param name="stockQuantity">Quantity compared with the minimum. Warehouse products use stock minus reserved.</param>
    /// <param name="minStockQuantity">Minimum stock quantity</param>
    /// <param name="activity">Low stock activity of the product</param>
    public LowStockRow(int productId, int? combinationId, string name, int stockQuantity, int minStockQuantity, LowStockActivity activity)
    {
        ProductId = productId;
        CombinationId = combinationId;
        Name = name;
        StockQuantity = stockQuantity;
        MinStockQuantity = minStockQuantity;
        Activity = activity;
    }

    /// <summary>
    /// Gets or sets the product identifier
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Gets or sets the combination identifier; null for a product row
    /// </summary>
    public int? CombinationId { get; set; }

    /// <summary>
    /// Gets or sets the product name. Combination rows use the parent product name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the quantity that was compared with <see cref="MinStockQuantity"/>
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>
    /// Gets or sets the minimum stock quantity
    /// </summary>
    public int MinStockQuantity { get; set; }

    /// <summary>
    /// Gets or sets the product low stock activity. Combination rows use the parent product.
    /// </summary>
    public LowStockActivity Activity { get; set; }
}

/// <summary>
/// An order whose payment is still pending and was placed before the pending-age threshold.
/// </summary>
public partial class PendingPaymentRow
{
    /// <summary>
    /// Ctor
    /// </summary>
    public PendingPaymentRow()
    {
    }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="customOrderNumber">Custom order number</param>
    /// <param name="orderTotal">Order total</param>
    /// <param name="createdOnUtc">Creation date (UTC)</param>
    public PendingPaymentRow(int orderId, string customOrderNumber, decimal orderTotal, DateTime createdOnUtc)
    {
        OrderId = orderId;
        CustomOrderNumber = customOrderNumber;
        OrderTotal = orderTotal;
        CreatedOnUtc = createdOnUtc;
    }

    /// <summary>
    /// Gets or sets the order identifier
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Gets or sets the custom order number
    /// </summary>
    public string CustomOrderNumber { get; set; }

    /// <summary>
    /// Gets or sets the order total
    /// </summary>
    public decimal OrderTotal { get; set; }

    /// <summary>
    /// Gets or sets the creation date (UTC)
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }
}

/// <summary>
/// A keyword whose recorded searches returned no products inside the search window.
/// </summary>
public partial class ZeroResultSearchRow
{
    /// <summary>
    /// Ctor
    /// </summary>
    public ZeroResultSearchRow()
    {
    }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="keyword">Search keyword</param>
    /// <param name="count">Number of zero-result searches for that keyword</param>
    public ZeroResultSearchRow(string keyword, int count)
    {
        Keyword = keyword;
        Count = count;
    }

    /// <summary>
    /// Gets or sets the search keyword
    /// </summary>
    public string Keyword { get; set; }

    /// <summary>
    /// Gets or sets how many zero-result searches used this keyword
    /// </summary>
    public int Count { get; set; }
}
