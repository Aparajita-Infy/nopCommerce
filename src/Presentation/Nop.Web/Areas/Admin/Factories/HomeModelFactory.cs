using System.Globalization;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Payments;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.StorePulse;
using Nop.Web.Areas.Admin.Infrastructure.Cache;
using Nop.Web.Areas.Admin.Models.Common;
using Nop.Web.Areas.Admin.Models.Home;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Areas.Admin.Models.Reports;
using Nop.Web.Framework;
using Nop.Web.Framework.Models.DataTables;
using ILogger = Nop.Services.Logging.ILogger;

namespace Nop.Web.Areas.Admin.Factories;

/// <summary>
/// Represents the home models factory implementation
/// </summary>
public partial class HomeModelFactory : IHomeModelFactory
{
    #region Fields

    protected readonly AdminAreaSettings _adminAreaSettings;
    protected readonly ICommonModelFactory _commonModelFactory;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly ILogger _logger;
    protected readonly IOrderModelFactory _orderModelFactory;
    protected readonly IPermissionService _permissionService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly ISettingService _settingService;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IStoreContext _storeContext;
    protected readonly IStorePulseService _storePulseService;
    protected readonly IWorkContext _workContext;
    protected readonly NopHttpClient _nopHttpClient;

    #endregion

    #region Ctor

    public HomeModelFactory(AdminAreaSettings adminAreaSettings,
        ICommonModelFactory commonModelFactory,
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        ILogger logger,
        IOrderModelFactory orderModelFactory,
        IPermissionService permissionService,
        IPriceFormatter priceFormatter,
        ISettingService settingService,
        IStaticCacheManager staticCacheManager,
        IStoreContext storeContext,
        IStorePulseService storePulseService,
        IWorkContext workContext,
        NopHttpClient nopHttpClient)
    {
        _adminAreaSettings = adminAreaSettings;
        _commonModelFactory = commonModelFactory;
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _logger = logger;
        _orderModelFactory = orderModelFactory;
        _permissionService = permissionService;
        _priceFormatter = priceFormatter;
        _settingService = settingService;
        _staticCacheManager = staticCacheManager;
        _storeContext = storeContext;
        _storePulseService = storePulseService;
        _workContext = workContext;
        _nopHttpClient = nopHttpClient;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepare dashboard model
    /// </summary>
    /// <param name="model">Dashboard model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the dashboard model
    /// </returns>
    public virtual async Task<DashboardModel> PrepareDashboardModelAsync(DashboardModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.IsLoggedInAsVendor = await _workContext.GetCurrentVendorAsync() != null;

        return model;
    }

    /// <summary>
    /// Prepare popular search term report model
    /// </summary>
    /// <param name="model">DataTables model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the dashboard model
    /// </returns>
    public virtual async Task<DataTablesModel> PreparePopularSearchTermReportModelAsync(DataTablesModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var searchModel = new PopularSearchTermSearchModel();
        searchModel = await _commonModelFactory.PreparePopularSearchTermSearchModelAsync(searchModel);

        model.Name = "search-term-report-grid";
        model.UrlRead = new DataUrl("PopularSearchTermsReport", "Common", null);
        model.Length = searchModel.Length;
        model.LengthMenu = searchModel.AvailablePageSizes;
        model.RefreshButton = false;
        model.ColumnCollection = new List<ColumnProperty>
        {
            new(nameof(PopularSearchTermModel.Keyword))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SearchTermReport.Keyword")
            },
            new(nameof(PopularSearchTermModel.Count))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SearchTermReport.Count")
            }
        };

        return model;
    }

    /// <summary>
    /// Prepare bestsellers brief by amount report model
    /// </summary>
    /// <param name="model">DataTables model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the dashboard model
    /// </returns>
    public virtual async Task<DataTablesModel> PrepareBestsellersBriefReportByAmountModelAsync(DataTablesModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var searchModel = new BestsellerBriefSearchModel();
        searchModel = await _orderModelFactory.PrepareBestsellerBriefSearchModelAsync(searchModel);

        model.Name = "bestsellers-byamount-grid";
        model.UrlRead = new DataUrl("BestsellersBriefReportByAmountList", "Order", new RouteValueDictionary { [nameof(searchModel.OrderBy)] = OrderByEnum.OrderByTotalAmount });
        model.Length = searchModel.PageSize;
        model.RefreshButton = false;
        model.ColumnCollection = new List<ColumnProperty>
        {
            new(nameof(BestsellerModel.ProductName))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Reports.Sales.Bestsellers.Fields.Name")
            },
            new(nameof(BestsellerModel.TotalQuantity))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Reports.Sales.Bestsellers.Fields.TotalQuantity")
            },
            new(nameof(BestsellerModel.TotalAmount))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Reports.Sales.Bestsellers.Fields.TotalAmount")
            },
            new(nameof(BestsellerModel.ProductId))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Common.View"),
                Width = "80",
                ClassName = NopColumnClassDefaults.Button,
                Render = new RenderButtonView(new DataUrl("~/Admin/Product/Edit/"))
            }
        };

        return model;
    }

    /// <summary>
    /// Prepare bestsellers brief by quantity report model
    /// </summary>
    /// <param name="model">DataTables model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the dashboard model
    /// </returns>
    public virtual async Task<DataTablesModel> PrepareBestsellersBriefReportByQuantityModelAsync(DataTablesModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var searchModel = new BestsellerBriefSearchModel();
        searchModel = await _orderModelFactory.PrepareBestsellerBriefSearchModelAsync(searchModel);

        model.Name = "bestsellers-byquantity-grid";
        model.UrlRead = new DataUrl("BestsellersBriefReportByQuantityList", "Order", new RouteValueDictionary { [nameof(searchModel.OrderBy)] = OrderByEnum.OrderByQuantity });
        model.Length = searchModel.PageSize;
        model.RefreshButton = false;        
        model.ColumnCollection = new List<ColumnProperty>
        {
            new(nameof(BestsellerModel.ProductName))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Reports.Sales.Bestsellers.Fields.Name")
            },
            new(nameof(BestsellerModel.TotalQuantity))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Reports.Sales.Bestsellers.Fields.TotalQuantity")
            },
            new(nameof(BestsellerModel.TotalAmount))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Reports.Sales.Bestsellers.Fields.TotalAmount")
            },
            new(nameof(BestsellerModel.ProductId))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Common.View"),
                Width = "80",
                ClassName = NopColumnClassDefaults.Button,
                Render = new RenderButtonView(new DataUrl("~/Admin/Product/Edit/"))
            }
        };

        return model;
    }


    /// <summary>
    /// Prepare latest orders model
    /// </summary>
    /// <param name="model">DataTables model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the dashboard model
    /// </returns>
    public virtual async Task<DataTablesModel> PrepareLatestOrdersModelAsync(DataTablesModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Name = "orders-grid";
        model.UrlRead = new DataUrl("OrderList", "Order", null);
        model.Length = 5;
        model.RefreshButton = false;        
        model.ColumnCollection = new List<ColumnProperty>
        {
            new(nameof(OrderModel.CustomOrderNumber))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Orders.Fields.CustomOrderNumber"),
                Width = "80"
            },
            new(nameof(OrderModel.OrderStatus))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Orders.Fields.OrderStatus"),
                Width = "100",
                Render = new RenderCustom("renderColumnOrderStatus")
            },
            new(nameof(OrderModel.CustomerEmail))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Orders.Fields.Customer"),
                Width = "250",
                Render = new RenderCustom("renderColumnCustomerEmail")
            },
            new(nameof(OrderModel.CreatedOn))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Orders.Fields.CreatedOn"),
                Width = "100",
                Render = new RenderDate()
            },
            new(nameof(OrderModel.Id))
            {
                Title = await _localizationService.GetResourceAsync("Admin.Common.View"),
                Width = "50",
                ClassName = NopColumnClassDefaults.Button,
                Render = new RenderButtonView(new DataUrl("~/Admin/Order/Edit/"))
            }
        };

        return model;
    }

    /// <summary>
    /// Prepare incomplete orders report model
    /// </summary>
    /// <param name="model">DataTables model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the dashboard model
    /// </returns>
    public virtual async Task<DataTablesModel> PrepareOrderIncompleteModelAsync(DataTablesModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Name = "incomplete-order-report-grid";
        model.UrlRead = new DataUrl("OrderIncompleteReportList", "Order", null);
        model.Length = int.MaxValue;
        model.Paging = false;
        model.Info = false;
        model.ColumnCollection = new List<ColumnProperty>
        {
            new(nameof(OrderIncompleteReportModel.Item))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SalesReport.Incomplete.Item")
            },
            new(nameof(OrderIncompleteReportModel.Total))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SalesReport.Incomplete.Total"),
                Width = "50"
            },
            new(nameof(OrderIncompleteReportModel.Count))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SalesReport.Incomplete.Count"),
                Width = "120",
                ClassName =  NopColumnClassDefaults.Button,
                Render = new RenderCustom("renderColumnOrderIncompleteReportCount")
            }
        };

        return model;
    }

    /// <summary>
    /// Prepare order average report model
    /// </summary>
    /// <param name="model">DataTables model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the dashboard model
    /// </returns>
    public virtual async Task<DataTablesModel> PrepareOrderAverageModelAsync(DataTablesModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Name = "average-order-report-grid";
        model.UrlRead = new DataUrl("OrderAverageReportList", "Order", null);
        model.Length = int.MaxValue;
        model.Paging = false;
        model.Info = false;
        model.ColumnCollection = new List<ColumnProperty>
        {
            new(nameof(OrderAverageReportModel.OrderStatus))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SalesReport.Average.OrderStatus")
            },
            new(nameof(OrderAverageReportModel.SumTodayOrders))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SalesReport.Average.SumTodayOrders")
            },
            new(nameof(OrderAverageReportModel.SumThisWeekOrders))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SalesReport.Average.SumThisWeekOrders")
            },
            new(nameof(OrderAverageReportModel.SumThisMonthOrders))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SalesReport.Average.SumThisMonthOrders")
            },
            new(nameof(OrderAverageReportModel.SumThisYearOrders))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SalesReport.Average.SumThisYearOrders")
            },
            new(nameof(OrderAverageReportModel.SumAllTimeOrders))
            {
                Title = await _localizationService.GetResourceAsync("Admin.SalesReport.Average.SumAllTimeOrders")
            }
        };

        return model;
    }

    /// <summary>
    /// Prepare nopCommerce news model
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the nopCommerce news model
    /// </returns>
    public virtual async Task<NopCommerceNewsModel> PrepareNopCommerceNewsModelAsync()
    {
        var model = new NopCommerceNewsModel
        {
            HideAdvertisements = _adminAreaSettings.HideAdvertisementsOnAdminArea
        };

        try
        {
            //try to get news RSS feed
            var rssData = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(NopModelCacheDefaults.OfficialNewsModelKey), async () =>
            {
                try
                {
                    return await _nopHttpClient.GetNewsRssAsync();
                }
                catch (AggregateException exception)
                {
                    //rethrow actual excepion
                    throw exception.InnerException;
                }
            });

            for (var i = 0; i < rssData.Items.Count; i++)
            {
                var item = rssData.Items.ElementAt(i);
                var newsItem = new NopCommerceNewsDetailsModel
                {
                    Title = item.TitleText,
                    Summary = XmlHelper.XmlDecode(item.Content?.Value ?? string.Empty),
                    Url = item.Url.OriginalString,
                    PublishDate = item.PublishDate
                };
                model.Items.Add(newsItem);

                //has new items?
                if (i != 0)
                    continue;

                var firstRequest = string.IsNullOrEmpty(_adminAreaSettings.LastNewsTitleAdminArea);
                if (_adminAreaSettings.LastNewsTitleAdminArea == newsItem.Title)
                    continue;

                _adminAreaSettings.LastNewsTitleAdminArea = newsItem.Title;
                await _settingService.SaveSettingAsync(_adminAreaSettings);

                //new item
                if (!firstRequest)
                    model.HasNewItems = true;
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("No access to the news. Website www.nopcommerce.com is not available.", ex);
        }

        return model;
    }

    /// <summary>
    /// Prepare the Store Pulse card. Vendors get an empty model. Each tile is included only when
    /// the current user has the permissions of that tile's admin screens. One tile's failure
    /// does not remove the others.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the Store Pulse model
    /// </returns>
    public virtual async Task<StorePulseModel> PrepareStorePulseModelAsync()
    {
        var model = new StorePulseModel
        {
            IsLoggedInAsVendor = await _workContext.GetCurrentVendorAsync() != null
        };

        if (model.IsLoggedInAsVendor)
            return model;

        // 0 is every store. The same value GetActiveStoreScopeConfigurationAsync returns for "All stores".
        var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();

        if (await AuthorizeAllAsync(StandardPermission.Orders.CURRENT_CARTS_MANAGE, StandardPermission.Customers.CUSTOMERS_VIEW))
            model.AbandonedCarts = await PrepareAbandonedCartsTileAsync(storeId);

        if (await AuthorizeAllAsync(StandardPermission.Reports.LOW_STOCK, StandardPermission.Catalog.PRODUCTS_VIEW))
            model.LowStock = await PrepareLowStockTileAsync();

        if (await _permissionService.AuthorizeAsync(StandardPermission.Orders.ORDERS_VIEW))
            model.PendingPayments = await PreparePendingPaymentsTileAsync(storeId);

        if (await _permissionService.AuthorizeAsync(StandardPermission.Catalog.PRODUCTS_VIEW))
            model.ZeroResultSearches = await PrepareZeroResultSearchesTileAsync(storeId);

        return model;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Requires every permission. ReportController.LowStock uses two
    /// <c>CheckPermission</c> filters, and each filter must pass.
    /// </summary>
    /// <param name="permissions">Permission system names</param>
    /// <returns>True when the current user has all of them</returns>
    protected virtual async Task<bool> AuthorizeAllAsync(params string[] permissions)
    {
        foreach (var permission in permissions)
        {
            if (!await _permissionService.AuthorizeAsync(permission))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Admin area path. Query values are encoded so a keyword cannot add another parameter.
    /// </summary>
    /// <param name="controller">Controller name</param>
    /// <param name="action">Action name</param>
    /// <param name="id">Optional route id</param>
    /// <param name="query">Optional query string</param>
    /// <returns>Root-relative admin path</returns>
    protected virtual string GetAdminPath(string controller, string action, int? id = null, IReadOnlyDictionary<string, string> query = null)
    {
        var path = $"/{AreaNames.ADMIN}/{controller}/{action}";
        if (id.HasValue)
            path += $"/{id.Value}";

        if (query == null || query.Count == 0)
            return path;

        var pairs = query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}");

        return $"{path}?{string.Join("&", pairs)}";
    }

    /// <summary>
    /// Converts a UTC instant to the admin user's time zone and drops the kind so the view
    /// prints that clock time instead of shifting it again.
    /// </summary>
    /// <param name="utc">UTC value</param>
    /// <returns>User-local clock time</returns>
    protected virtual async Task<DateTime> ToUserTimeAsync(DateTime utc)
    {
        var userTime = await _dateTimeHelper.ConvertToUserTimeAsync(utc, DateTimeKind.Utc);

        return DateTime.SpecifyKind(userTime, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Abandoned carts. Current carts needs <see cref="StandardPermission.Orders.CURRENT_CARTS_MANAGE"/>.
    /// Each row opens customer edit, which needs <see cref="StandardPermission.Customers.CUSTOMERS_VIEW"/>.
    /// </summary>
    /// <param name="storeId">Active store scope</param>
    /// <returns>Tile</returns>
    protected virtual async Task<StorePulseAbandonedCartsModel> PrepareAbandonedCartsTileAsync(int storeId)
    {
        var model = new StorePulseAbandonedCartsModel
        {
            Visible = true,
            ViewAllUrl = GetAdminPath("ShoppingCart", "CurrentCarts")
        };

        try
        {
            var tile = await _storePulseService.GetAbandonedCartsAsync(storeId) ?? new PulseTile<AbandonedCartRow>();
            model.TotalCount = tile.TotalCount;

            foreach (var row in (tile.TopRows ?? new List<AbandonedCartRow>()).Take(StorePulseDefaults.TopRows))
            {
                model.Rows.Add(new StorePulseAbandonedCartRowModel
                {
                    Email = row.Email,
                    ItemCount = row.ItemCount,
                    LastUpdated = await ToUserTimeAsync(row.LastUpdatedUtc),
                    Url = GetAdminPath("Customer", "Edit", row.CustomerId)
                });
            }
        }
        catch (Exception exception)
        {
            model.LoadFailed = true;
            await _logger.ErrorAsync("Store Pulse abandoned carts failed to load.", exception);
        }

        return model;
    }

    /// <summary>
    /// Low stock. The report action requires both <see cref="StandardPermission.Reports.LOW_STOCK"/>
    /// and <see cref="StandardPermission.Catalog.PRODUCTS_VIEW"/>. Combination rows open the parent product.
    /// </summary>
    /// <returns>Tile</returns>
    protected virtual async Task<StorePulseLowStockModel> PrepareLowStockTileAsync()
    {
        var model = new StorePulseLowStockModel
        {
            Visible = true,
            ViewAllUrl = GetAdminPath("Report", "LowStock")
        };

        try
        {
            var tile = await _storePulseService.GetLowStockAsync() ?? new PulseTile<LowStockRow>();
            model.TotalCount = tile.TotalCount;

            foreach (var row in (tile.TopRows ?? new List<LowStockRow>()).Take(StorePulseDefaults.TopRows))
            {
                model.Rows.Add(new StorePulseLowStockRowModel
                {
                    Name = row.Name,
                    StockQuantity = row.StockQuantity,
                    MinStockQuantity = row.MinStockQuantity,
                    Activity = await _localizationService.GetLocalizedEnumAsync(row.Activity),
                    Url = GetAdminPath("Product", "Edit", row.ProductId)
                });
            }
        }
        catch (Exception exception)
        {
            model.LoadFailed = true;
            await _logger.ErrorAsync("Store Pulse low stock failed to load.", exception);
        }

        return model;
    }

    /// <summary>
    /// Pending payments. Order list and order edit both require <see cref="StandardPermission.Orders.ORDERS_VIEW"/>.
    /// View all opens every pending order (<see cref="PaymentStatus.Pending"/> is 10), including orders newer than 24 hours.
    /// </summary>
    /// <param name="storeId">Active store scope</param>
    /// <returns>Tile</returns>
    protected virtual async Task<StorePulsePendingPaymentsModel> PreparePendingPaymentsTileAsync(int storeId)
    {
        var model = new StorePulsePendingPaymentsModel
        {
            Visible = true,
            ViewAllUrl = GetAdminPath("Order", "List", query: new Dictionary<string, string>
            {
                ["paymentStatuses"] = ((int)PaymentStatus.Pending).ToString(CultureInfo.InvariantCulture)
            })
        };

        try
        {
            var tile = await _storePulseService.GetPendingPaymentsAsync(storeId) ?? new PulseTile<PendingPaymentRow>();
            model.TotalCount = tile.TotalCount;

            foreach (var row in (tile.TopRows ?? new List<PendingPaymentRow>()).Take(StorePulseDefaults.TopRows))
            {
                model.Rows.Add(new StorePulsePendingPaymentRowModel
                {
                    CustomOrderNumber = row.CustomOrderNumber,
                    OrderTotal = await _priceFormatter.FormatPriceAsync(row.OrderTotal, true, false),
                    CreatedOn = await ToUserTimeAsync(row.CreatedOnUtc),
                    Url = GetAdminPath("Order", "Edit", row.OrderId)
                });
            }
        }
        catch (Exception exception)
        {
            model.LoadFailed = true;
            await _logger.ErrorAsync("Store Pulse pending payments failed to load.", exception);
        }

        return model;
    }

    /// <summary>
    /// Zero-result searches. The product list requires <see cref="StandardPermission.Catalog.PRODUCTS_VIEW"/>,
    /// the same permission as the popular search terms card. There is no view-all screen.
    /// </summary>
    /// <param name="storeId">Active store scope</param>
    /// <returns>Tile</returns>
    protected virtual async Task<StorePulseZeroResultSearchesModel> PrepareZeroResultSearchesTileAsync(int storeId)
    {
        var model = new StorePulseZeroResultSearchesModel
        {
            Visible = true
        };

        try
        {
            var tile = await _storePulseService.GetZeroResultSearchesAsync(storeId) ?? new PulseTile<ZeroResultSearchRow>();
            model.TotalCount = tile.TotalCount;

            foreach (var row in (tile.TopRows ?? new List<ZeroResultSearchRow>()).Take(StorePulseDefaults.TopRows))
            {
                model.Rows.Add(new StorePulseZeroResultSearchRowModel
                {
                    Keyword = row.Keyword,
                    Count = row.Count,
                    Url = GetAdminPath("Product", "List", query: new Dictionary<string, string>
                    {
                        ["q"] = row.Keyword ?? string.Empty
                    })
                });
            }
        }
        catch (Exception exception)
        {
            model.LoadFailed = true;
            await _logger.ErrorAsync("Store Pulse zero-result searches failed to load.", exception);
        }

        return model;
    }

    #endregion
}