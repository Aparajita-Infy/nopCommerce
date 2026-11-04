using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Customers;

namespace Nop.Services.StorePulse;

/// <summary>
/// Store Pulse tiles. Results are cached per tile and store for <see cref="StorePulseDefaults.TileCacheTime"/> minutes.
/// </summary>
public partial class StorePulseService : IStorePulseService
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly IProductService _productService;
    protected readonly IRepository<Customer> _customerRepository;
    protected readonly IRepository<CustomerCustomerRoleMapping> _customerRoleMappingRepository;
    protected readonly IRepository<Order> _orderRepository;
    protected readonly IRepository<SearchTerm> _searchTermRepository;
    protected readonly IRepository<ShoppingCartItem> _shoppingCartItemRepository;
    protected readonly IStaticCacheManager _staticCacheManager;

    #endregion

    #region Ctor

    public StorePulseService(ICustomerService customerService,
        IProductService productService,
        IRepository<Customer> customerRepository,
        IRepository<CustomerCustomerRoleMapping> customerRoleMappingRepository,
        IRepository<Order> orderRepository,
        IRepository<SearchTerm> searchTermRepository,
        IRepository<ShoppingCartItem> shoppingCartItemRepository,
        IStaticCacheManager staticCacheManager)
    {
        _customerService = customerService;
        _productService = productService;
        _customerRepository = customerRepository;
        _customerRoleMappingRepository = customerRoleMappingRepository;
        _orderRepository = orderRepository;
        _searchTermRepository = searchTermRepository;
        _shoppingCartItemRepository = shoppingCartItemRepository;
        _staticCacheManager = staticCacheManager;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Low stock has no store dimension, so every caller shares one cache entry.
    /// </summary>
    protected const int LowStockCacheStoreId = 0;

    /// <summary>
    /// Gets a cached tile. <see cref="IStaticCacheManager.PrepareKeyForDefaultCache"/> replaces
    /// <see cref="CacheKey.CacheTime"/> with the application default (60 minutes), so the tile TTL is applied after that.
    /// </summary>
    /// <typeparam name="T">Row type</typeparam>
    /// <param name="tile">Tile name</param>
    /// <param name="storeId">Store identifier used in the cache key</param>
    /// <param name="acquire">Loads the tile on a cache miss</param>
    /// <returns>Cached tile</returns>
    protected virtual async Task<PulseTile<T>> GetCachedTileAsync<T>(string tile, int storeId, Func<Task<PulseTile<T>>> acquire)
    {
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(StorePulseDefaults.TileCacheKey, tile, storeId);
        cacheKey.CacheTime = StorePulseDefaults.TileCacheTime;

        return await _staticCacheManager.GetAsync(cacheKey, acquire);
    }

    /// <summary>
    /// 0 (and any non-positive id) is the admin "all stores" scope from <c>GetActiveStoreScopeConfigurationAsync</c>.
    /// A single-store install always returns 0, so the store filter has to be optional here.
    /// </summary>
    /// <param name="storeId">Requested store identifier</param>
    /// <returns>Positive store identifier, or 0 for every store</returns>
    protected static int NormalizeStoreId(int storeId)
    {
        return storeId > 0 ? storeId : 0;
    }

    /// <summary>
    /// Loads abandoned carts for registered customers.
    /// A cart counts when its latest shopping-cart item was updated more than <see cref="StorePulseDefaults.CartIdle"/>
    /// ago and less than <see cref="StorePulseDefaults.CartMaxAge"/> ago. Wishlists and other cart types are ignored.
    /// Item count is the sum of quantities, including lines older than the window, because the window applies to the latest update.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 includes every store</param>
    /// <returns>Tile</returns>
    protected virtual async Task<PulseTile<AbandonedCartRow>> LoadAbandonedCartsAsync(int storeId)
    {
        var registeredRole = await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.RegisteredRoleName);
        if (registeredRole == null)
            return new PulseTile<AbandonedCartRow>();

        var utcNow = DateTime.UtcNow;
        var updatedBeforeUtc = utcNow - StorePulseDefaults.CartIdle;
        var updatedAfterUtc = utcNow - StorePulseDefaults.CartMaxAge;

        var items = _shoppingCartItemRepository.Table
            .Where(item => item.ShoppingCartTypeId == (int)ShoppingCartType.ShoppingCart);

        if (storeId > 0)
            items = items.Where(item => item.StoreId == storeId);

        // Exists, not a join: a duplicated role mapping must not double-count quantities.
        items = items.Where(item => _customerRoleMappingRepository.Table.Any(mapping =>
            mapping.CustomerId == item.CustomerId && mapping.CustomerRoleId == registeredRole.Id));

        var grouped =
            from item in items
            group item by item.CustomerId
            into customerItems
            select new
            {
                CustomerId = customerItems.Key,
                LastUpdatedUtc = customerItems.Max(item => item.UpdatedOnUtc),
                ItemCount = customerItems.Sum(item => item.Quantity)
            };

        var abandoned = grouped.Where(row =>
            row.LastUpdatedUtc < updatedBeforeUtc && row.LastUpdatedUtc > updatedAfterUtc);

        var totalCount = await abandoned.CountAsync();
        var top = await abandoned
            .OrderByDescending(row => row.LastUpdatedUtc)
            .ThenBy(row => row.CustomerId)
            .Take(StorePulseDefaults.TopRows)
            .ToListAsync();

        if (top.Count == 0)
            return new PulseTile<AbandonedCartRow>(totalCount, new List<AbandonedCartRow>());

        var customerIds = top.Select(row => row.CustomerId).ToArray();
        var customers = await _customerRepository.Table
            .Where(customer => customerIds.Contains(customer.Id))
            .Select(customer => new { customer.Id, customer.Email })
            .ToListAsync();
        var emailByCustomerId = customers.ToDictionary(customer => customer.Id, customer => customer.Email ?? string.Empty);

        var rows = top.Select(row => new AbandonedCartRow(
            row.CustomerId,
            emailByCustomerId.TryGetValue(row.CustomerId, out var email) ? email : string.Empty,
            row.ItemCount,
            row.LastUpdatedUtc)).ToList();

        return new PulseTile<AbandonedCartRow>(totalCount, rows);
    }

    /// <summary>
    /// Loads the low-stock tile from the existing product and combination queries.
    /// Published and unpublished rows are both included. The total is the sum of the two query totals.
    /// Rows are the first page of products, in that query's order, then the first page of combinations, truncated to the top-row limit.
    /// </summary>
    /// <returns>Tile</returns>
    protected virtual async Task<PulseTile<LowStockRow>> LoadLowStockAsync()
    {
        var products = await _productService.GetLowStockProductsAsync(
            vendorId: null,
            loadPublishedOnly: null,
            pageIndex: 0,
            pageSize: StorePulseDefaults.TopRows);
        var combinations = await _productService.GetLowStockProductCombinationsAsync(
            vendorId: null,
            loadPublishedOnly: null,
            pageIndex: 0,
            pageSize: StorePulseDefaults.TopRows);

        var rows = new List<LowStockRow>(StorePulseDefaults.TopRows);

        foreach (var product in products)
        {
            rows.Add(new LowStockRow(
                product.Id,
                null,
                product.Name ?? string.Empty,
                await _productService.GetTotalStockQuantityAsync(product),
                product.MinStockQuantity,
                product.LowStockActivity));
        }

        if (rows.Count < StorePulseDefaults.TopRows && combinations.Count > 0)
        {
            var parents = await _productService.GetProductsByIdsAsync(combinations.Select(combination => combination.ProductId).Distinct().ToArray());
            var parentById = parents.ToDictionary(product => product.Id);

            foreach (var combination in combinations)
            {
                if (rows.Count == StorePulseDefaults.TopRows)
                    break;

                parentById.TryGetValue(combination.ProductId, out var parent);
                rows.Add(new LowStockRow(
                    combination.ProductId,
                    combination.Id,
                    parent?.Name ?? string.Empty,
                    combination.StockQuantity,
                    combination.MinStockQuantity,
                    parent?.LowStockActivity ?? LowStockActivity.Nothing));
            }
        }

        return new PulseTile<LowStockRow>(products.TotalCount + combinations.TotalCount, rows);
    }

    /// <summary>
    /// Loads orders with <see cref="PaymentStatus.Pending"/> created before the pending-age threshold.
    /// Voided and other payment statuses are excluded because they are not pending. Deleted orders are excluded.
    /// Order status is not part of the rule. Oldest orders come first.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 includes every store</param>
    /// <returns>Tile</returns>
    protected virtual async Task<PulseTile<PendingPaymentRow>> LoadPendingPaymentsAsync(int storeId)
    {
        var createdBeforeUtc = DateTime.UtcNow - StorePulseDefaults.PendingAge;
        var orders = _orderRepository.Table.Where(order =>
            !order.Deleted &&
            order.PaymentStatusId == (int)PaymentStatus.Pending &&
            order.CreatedOnUtc < createdBeforeUtc);

        if (storeId > 0)
            orders = orders.Where(order => order.StoreId == storeId);

        var totalCount = await orders.CountAsync();
        var top = await orders
            .OrderBy(order => order.CreatedOnUtc)
            .ThenBy(order => order.Id)
            .Take(StorePulseDefaults.TopRows)
            .ToListAsync();

        var rows = top.Select(order => new PendingPaymentRow(
            order.Id,
            order.CustomOrderNumber ?? string.Empty,
            order.OrderTotal,
            order.CreatedOnUtc)).ToList();

        return new PulseTile<PendingPaymentRow>(totalCount, rows);
    }

    /// <summary>
    /// Loads keywords with <c>ResultCount == 0</c> inside the search window.
    /// Null counts were recorded before the column existed and are excluded. Deleted terms are excluded.
    /// The total is the number of distinct keywords, not the number of search rows. Most frequent keywords come first.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 groups keywords across every store</param>
    /// <returns>Tile</returns>
    protected virtual async Task<PulseTile<ZeroResultSearchRow>> LoadZeroResultSearchesAsync(int storeId)
    {
        var createdAfterUtc = DateTime.UtcNow - StorePulseDefaults.SearchWindow;
        var terms = _searchTermRepository.Table.Where(term =>
            !term.Deleted &&
            term.ResultCount == 0 &&
            term.CreatedOnUtc > createdAfterUtc);

        if (storeId > 0)
            terms = terms.Where(term => term.StoreId == storeId);

        var grouped =
            from term in terms
            group term by term.Keyword
            into keywordGroup
            select new
            {
                Keyword = keywordGroup.Key,
                Count = keywordGroup.Count()
            };

        var totalCount = await grouped.CountAsync();
        var top = await grouped
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.Keyword)
            .Take(StorePulseDefaults.TopRows)
            .ToListAsync();

        var rows = top.Select(row => new ZeroResultSearchRow(row.Keyword ?? string.Empty, row.Count)).ToList();

        return new PulseTile<ZeroResultSearchRow>(totalCount, rows);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets abandoned shopping carts for registered customers.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 includes every store</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the total and the most recently updated carts.
    /// </returns>
    public virtual Task<PulseTile<AbandonedCartRow>> GetAbandonedCartsAsync(int storeId)
    {
        var scope = NormalizeStoreId(storeId);

        return GetCachedTileAsync(StorePulseDefaults.AbandonedCartsTile, scope, () => LoadAbandonedCartsAsync(scope));
    }

    /// <summary>
    /// Gets products and combinations at or below their minimum stock. Not filtered by store.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the combined total and the first rows of each low-stock query.
    /// </returns>
    public virtual Task<PulseTile<LowStockRow>> GetLowStockAsync()
    {
        return GetCachedTileAsync(StorePulseDefaults.LowStockTile, LowStockCacheStoreId, LoadLowStockAsync);
    }

    /// <summary>
    /// Gets orders whose payment is still pending and that were created before the pending-age threshold.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 includes every store</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the total and the oldest matching orders.
    /// </returns>
    public virtual Task<PulseTile<PendingPaymentRow>> GetPendingPaymentsAsync(int storeId)
    {
        var scope = NormalizeStoreId(storeId);

        return GetCachedTileAsync(StorePulseDefaults.PendingPaymentsTile, scope, () => LoadPendingPaymentsAsync(scope));
    }

    /// <summary>
    /// Gets keywords that returned no products inside the search window.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 includes every store. Keywords are grouped across the selected stores.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the number of distinct keywords and the most frequent ones.
    /// </returns>
    public virtual Task<PulseTile<ZeroResultSearchRow>> GetZeroResultSearchesAsync(int storeId)
    {
        var scope = NormalizeStoreId(storeId);

        return GetCachedTileAsync(StorePulseDefaults.ZeroResultSearchesTile, scope, () => LoadZeroResultSearchesAsync(scope));
    }

    #endregion
}
