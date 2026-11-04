using Nop.Web.Framework.Models;

namespace Nop.Web.Areas.Admin.Models.Home;

/// <summary>
/// Store Pulse card. Each tile is filled only when the current user may open that tile's screens.
/// </summary>
public partial record StorePulseModel : BaseNopModel
{
    #region Ctor

    public StorePulseModel()
    {
        AbandonedCarts = new StorePulseAbandonedCartsModel();
        LowStock = new StorePulseLowStockModel();
        PendingPayments = new StorePulsePendingPaymentsModel();
        ZeroResultSearches = new StorePulseZeroResultSearchesModel();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets a value indicating whether the current customer is a vendor.
    /// Vendors do not see the card.
    /// </summary>
    public bool IsLoggedInAsVendor { get; set; }

    public StorePulseAbandonedCartsModel AbandonedCarts { get; set; }

    public StorePulseLowStockModel LowStock { get; set; }

    public StorePulsePendingPaymentsModel PendingPayments { get; set; }

    public StorePulseZeroResultSearchesModel ZeroResultSearches { get; set; }

    /// <summary>
    /// Gets a value indicating whether any visible tile failed to load.
    /// </summary>
    public bool HasLoadFailure =>
        AbandonedCarts.LoadFailed || LowStock.LoadFailed || PendingPayments.LoadFailed || ZeroResultSearches.LoadFailed;

    #endregion
}

/// <summary>
/// Abandoned-cart tile.
/// </summary>
public partial record StorePulseAbandonedCartsModel : BaseNopModel
{
    #region Ctor

    public StorePulseAbandonedCartsModel()
    {
        Rows = new List<StorePulseAbandonedCartRowModel>();
    }

    #endregion

    #region Properties

    public bool Visible { get; set; }

    public bool LoadFailed { get; set; }

    public int TotalCount { get; set; }

    public string ViewAllUrl { get; set; }

    public IList<StorePulseAbandonedCartRowModel> Rows { get; set; }

    #endregion
}

/// <summary>
/// One abandoned cart.
/// </summary>
public partial record StorePulseAbandonedCartRowModel : BaseNopModel
{
    public string Email { get; set; }

    public int ItemCount { get; set; }

    /// <summary>
    /// Gets or sets the latest update in the admin user's time zone.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    public string Url { get; set; }
}

/// <summary>
/// Low-stock tile. Product and combination rows both link to the parent product.
/// </summary>
public partial record StorePulseLowStockModel : BaseNopModel
{
    #region Ctor

    public StorePulseLowStockModel()
    {
        Rows = new List<StorePulseLowStockRowModel>();
    }

    #endregion

    #region Properties

    public bool Visible { get; set; }

    public bool LoadFailed { get; set; }

    public int TotalCount { get; set; }

    public string ViewAllUrl { get; set; }

    public IList<StorePulseLowStockRowModel> Rows { get; set; }

    #endregion
}

/// <summary>
/// One low-stock product or combination.
/// </summary>
public partial record StorePulseLowStockRowModel : BaseNopModel
{
    public string Name { get; set; }

    public int StockQuantity { get; set; }

    public int MinStockQuantity { get; set; }

    public string Activity { get; set; }

    public string Url { get; set; }
}

/// <summary>
/// Pending-payment tile. The view-all list is every pending order, including ones newer than 24 hours.
/// </summary>
public partial record StorePulsePendingPaymentsModel : BaseNopModel
{
    #region Ctor

    public StorePulsePendingPaymentsModel()
    {
        Rows = new List<StorePulsePendingPaymentRowModel>();
    }

    #endregion

    #region Properties

    public bool Visible { get; set; }

    public bool LoadFailed { get; set; }

    public int TotalCount { get; set; }

    public string ViewAllUrl { get; set; }

    public IList<StorePulsePendingPaymentRowModel> Rows { get; set; }

    #endregion
}

/// <summary>
/// One order whose payment is still pending.
/// </summary>
public partial record StorePulsePendingPaymentRowModel : BaseNopModel
{
    public string CustomOrderNumber { get; set; }

    public string OrderTotal { get; set; }

    /// <summary>
    /// Gets or sets the creation date in the admin user's time zone.
    /// </summary>
    public DateTime CreatedOn { get; set; }

    public string Url { get; set; }
}

/// <summary>
/// Zero-result search tile. There is no view-all screen.
/// </summary>
public partial record StorePulseZeroResultSearchesModel : BaseNopModel
{
    #region Ctor

    public StorePulseZeroResultSearchesModel()
    {
        Rows = new List<StorePulseZeroResultSearchRowModel>();
    }

    #endregion

    #region Properties

    public bool Visible { get; set; }

    public bool LoadFailed { get; set; }

    public int TotalCount { get; set; }

    public IList<StorePulseZeroResultSearchRowModel> Rows { get; set; }

    #endregion
}

/// <summary>
/// One keyword that returned no products.
/// </summary>
public partial record StorePulseZeroResultSearchRowModel : BaseNopModel
{
    public string Keyword { get; set; }

    public int Count { get; set; }

    public string Url { get; set; }
}
