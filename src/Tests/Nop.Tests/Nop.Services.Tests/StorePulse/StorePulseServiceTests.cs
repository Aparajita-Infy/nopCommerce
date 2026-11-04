using AwesomeAssertions;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.StorePulse;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.StorePulse;

[TestFixture]
public class StorePulseServiceTests : ServiceTest
{
    private const int StoreA = 91001;
    private const int StoreB = 91002;

    private IStorePulseService _storePulseService;
    private IStaticCacheManager _staticCacheManager;
    private ICustomerService _customerService;
    private IProductService _productService;
    private IProductAttributeService _productAttributeService;
    private IRepository<ShoppingCartItem> _shoppingCartItemRepository;
    private IRepository<Order> _orderRepository;
    private IRepository<SearchTerm> _searchTermRepository;
    private IRepository<Customer> _customerRepository;
    private IRepository<CustomerCustomerRoleMapping> _roleMappingRepository;
    private IRepository<Product> _productRepository;
    private IRepository<ProductAttributeCombination> _combinationRepository;
    private IRepository<ProductWarehouseInventory> _warehouseInventoryRepository;

    private int _registeredRoleId;
    private int _guestRoleId;
    private Product _cartProduct;
    private readonly List<int> _customerIds = new();
    private readonly List<int> _productIds = new();

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _storePulseService = GetService<IStorePulseService>();
        _staticCacheManager = GetService<IStaticCacheManager>();
        _customerService = GetService<ICustomerService>();
        _productService = GetService<IProductService>();
        _productAttributeService = GetService<IProductAttributeService>();
        _shoppingCartItemRepository = GetService<IRepository<ShoppingCartItem>>();
        _orderRepository = GetService<IRepository<Order>>();
        _searchTermRepository = GetService<IRepository<SearchTerm>>();
        _customerRepository = GetService<IRepository<Customer>>();
        _roleMappingRepository = GetService<IRepository<CustomerCustomerRoleMapping>>();
        _productRepository = GetService<IRepository<Product>>();
        _combinationRepository = GetService<IRepository<ProductAttributeCombination>>();
        _warehouseInventoryRepository = GetService<IRepository<ProductWarehouseInventory>>();

        _registeredRoleId = (await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.RegisteredRoleName)).Id;
        _guestRoleId = (await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.GuestsRoleName)).Id;

        _cartProduct = new Product
        {
            Name = "Store pulse cart product",
            Published = true,
            ProductType = ProductType.SimpleProduct,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };
        await _productService.InsertProductAsync(_cartProduct);
    }

    [TearDown]
    public async Task TearDown()
    {
        await DeleteTrackedCustomersAsync();
        await DeleteTrackedProductsAsync();
        await ForgetTilesAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_cartProduct?.Id > 0)
            await _productRepository.DeleteAsync(product => product.Id == _cartProduct.Id);
    }

    #region Abandoned carts

    [Test]
    public async Task AbandonedCartsCountRegisteredShoppingCartsInsideTheWindow()
    {
        var now = DateTime.UtcNow;
        var idle = await AddCustomerAsync("pulse-cart-idle@example.com", registered: true);
        var recent = await AddCustomerAsync("pulse-cart-recent@example.com", registered: true);
        var ancient = await AddCustomerAsync("pulse-cart-ancient@example.com", registered: true);
        var refreshed = await AddCustomerAsync("pulse-cart-refreshed@example.com", registered: true);
        var guest = await AddCustomerAsync("pulse-cart-guest@example.com", registered: false);
        var wishlist = await AddCustomerAsync("pulse-cart-wishlist@example.com", registered: true);
        var otherStore = await AddCustomerAsync("pulse-cart-other@example.com", registered: true);

        var idleLatest = now.AddHours(-25);
        await AddCartAsync(idle, StoreA, ShoppingCartType.ShoppingCart, 2, idleLatest);
        await AddCartAsync(idle, StoreA, ShoppingCartType.ShoppingCart, 3, now.AddDays(-29));
        await AddCartAsync(idle, StoreA, ShoppingCartType.Wishlist, 9, idleLatest);
        await AddCartAsync(idle, StoreA, ShoppingCartType.Stash, 7, idleLatest);
        await AddCartAsync(recent, StoreA, ShoppingCartType.ShoppingCart, 1, now.AddHours(-23));
        await AddCartAsync(ancient, StoreA, ShoppingCartType.ShoppingCart, 1, now.AddDays(-31));
        await AddCartAsync(refreshed, StoreA, ShoppingCartType.ShoppingCart, 4, now.AddHours(-25));
        await AddCartAsync(refreshed, StoreA, ShoppingCartType.ShoppingCart, 1, now.AddHours(-23));
        await AddCartAsync(guest, StoreA, ShoppingCartType.ShoppingCart, 1, idleLatest);
        await AddCartAsync(wishlist, StoreA, ShoppingCartType.Wishlist, 1, idleLatest);
        await AddCartAsync(otherStore, StoreB, ShoppingCartType.ShoppingCart, 4, idleLatest);

        await ForgetAsync(StorePulseDefaults.AbandonedCartsTile, StoreA);
        var tile = await _storePulseService.GetAbandonedCartsAsync(StoreA);

        tile.TotalCount.Should().Be(1);
        var row = tile.TopRows.Should().ContainSingle().Subject;
        row.CustomerId.Should().Be(idle.Id);
        row.Email.Should().Be(idle.Email);
        row.ItemCount.Should().Be(5);
        row.LastUpdatedUtc.Should().BeCloseTo(idleLatest, TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task AbandonedCartsOrderByLatestUpdateAndKeepTheTotal()
    {
        var now = DateTime.UtcNow;
        var first = await AddCustomerAsync("pulse-cart-top-1@example.com", registered: true);
        var tiedEarlier = await AddCustomerAsync("pulse-cart-top-2@example.com", registered: true);
        var tiedLater = await AddCustomerAsync("pulse-cart-top-3@example.com", registered: true);
        var fourth = await AddCustomerAsync("pulse-cart-top-4@example.com", registered: true);
        var fifth = await AddCustomerAsync("pulse-cart-top-5@example.com", registered: true);
        var sixth = await AddCustomerAsync("pulse-cart-top-6@example.com", registered: true);
        var tiedAt = now.AddHours(-26);

        await AddCartAsync(first, StoreA, ShoppingCartType.ShoppingCart, 1, now.AddHours(-25));
        await AddCartAsync(tiedEarlier, StoreA, ShoppingCartType.ShoppingCart, 1, tiedAt);
        await AddCartAsync(tiedLater, StoreA, ShoppingCartType.ShoppingCart, 1, tiedAt);
        await AddCartAsync(fourth, StoreA, ShoppingCartType.ShoppingCart, 1, now.AddHours(-27));
        await AddCartAsync(fifth, StoreA, ShoppingCartType.ShoppingCart, 1, now.AddHours(-28));
        await AddCartAsync(sixth, StoreA, ShoppingCartType.ShoppingCart, 1, now.AddHours(-48));

        await ForgetAsync(StorePulseDefaults.AbandonedCartsTile, StoreA);
        var tile = await _storePulseService.GetAbandonedCartsAsync(StoreA);

        tile.TotalCount.Should().Be(6);
        tile.TopRows.Should().HaveCount(StorePulseDefaults.TopRows);
        tile.TopRows.Select(row => row.CustomerId).Should().Equal(
            first.Id, tiedEarlier.Id, tiedLater.Id, fourth.Id, fifth.Id);
        tile.TopRows.Select(row => row.CustomerId).Should().NotContain(sixth.Id);
    }

    [Test]
    public async Task AbandonedCartsFilterByStoreAndCachePerStore()
    {
        var updatedOnUtc = DateTime.UtcNow.AddHours(-25);
        var storeACustomer = await AddCustomerAsync("pulse-cart-store-a@example.com", registered: true);
        var storeBCustomer = await AddCustomerAsync("pulse-cart-store-b@example.com", registered: true);
        await AddCartAsync(storeACustomer, StoreA, ShoppingCartType.ShoppingCart, 1, updatedOnUtc);
        await AddCartAsync(storeBCustomer, StoreB, ShoppingCartType.ShoppingCart, 4, updatedOnUtc);

        await ForgetAsync(StorePulseDefaults.AbandonedCartsTile, StoreA);
        await ForgetAsync(StorePulseDefaults.AbandonedCartsTile, StoreB);
        await ForgetAsync(StorePulseDefaults.AbandonedCartsTile, 0);

        var storeA = await _storePulseService.GetAbandonedCartsAsync(StoreA);
        storeA.TopRows.Select(row => row.CustomerId).Should().Equal(storeACustomer.Id);

        var extra = await AddCustomerAsync("pulse-cart-store-a-extra@example.com", registered: true);
        await AddCartAsync(extra, StoreA, ShoppingCartType.ShoppingCart, 1, updatedOnUtc.AddHours(-1));

        var cached = await _storePulseService.GetAbandonedCartsAsync(StoreA);
        cached.TotalCount.Should().Be(storeA.TotalCount);
        cached.TopRows.Select(row => row.CustomerId).Should().Equal(storeACustomer.Id);

        var storeB = await _storePulseService.GetAbandonedCartsAsync(StoreB);
        storeB.TopRows.Select(row => row.CustomerId).Should().Equal(storeBCustomer.Id);
        storeB.TopRows.Single().ItemCount.Should().Be(4);

        var allStores = await _storePulseService.GetAbandonedCartsAsync(0);
        allStores.TotalCount.Should().Be(3);
        allStores.TopRows.Select(row => row.CustomerId).Should().BeEquivalentTo(new[] { storeACustomer.Id, storeBCustomer.Id, extra.Id });

        var negativeScope = await _storePulseService.GetAbandonedCartsAsync(-1);
        negativeScope.TotalCount.Should().Be(allStores.TotalCount);

        await ForgetAsync(StorePulseDefaults.AbandonedCartsTile, StoreA);
        var refreshed = await _storePulseService.GetAbandonedCartsAsync(StoreA);
        refreshed.TotalCount.Should().Be(2);
        refreshed.TopRows.Select(row => row.CustomerId).Should().BeEquivalentTo(new[] { storeACustomer.Id, extra.Id });
    }

    #endregion

    #region Pending payments

    [Test]
    public async Task PendingPaymentsCountOldPendingOrdersOnly()
    {
        var now = DateTime.UtcNow;
        var customer = await AddCustomerAsync("pulse-order@example.com", registered: true);
        var included = await AddOrderAsync(customer, StoreA, PaymentStatus.Pending, now.AddHours(-25), 19.5m, "P-25");
        var cancelledButPending = await AddOrderAsync(customer, StoreA, PaymentStatus.Pending, now.AddHours(-30), 8m, "P-30", OrderStatus.Cancelled);
        await AddOrderAsync(customer, StoreA, PaymentStatus.Pending, now.AddHours(-23), 1m, "P-23");
        await AddOrderAsync(customer, StoreA, PaymentStatus.Voided, now.AddHours(-48), 2m, "P-void");
        await AddOrderAsync(customer, StoreA, PaymentStatus.Paid, now.AddHours(-48), 3m, "P-paid");
        await AddOrderAsync(customer, StoreA, PaymentStatus.Pending, now.AddHours(-48), 4m, "P-deleted", deleted: true);
        await AddOrderAsync(customer, StoreB, PaymentStatus.Pending, now.AddHours(-48), 5m, "P-other");

        await ForgetAsync(StorePulseDefaults.PendingPaymentsTile, StoreA);
        var tile = await _storePulseService.GetPendingPaymentsAsync(StoreA);

        tile.TotalCount.Should().Be(2);
        tile.TopRows.Select(row => row.OrderId).Should().Equal(cancelledButPending.Id, included.Id);
        tile.TopRows[0].CustomOrderNumber.Should().Be("P-30");
        tile.TopRows[0].OrderTotal.Should().Be(8m);
        tile.TopRows[0].CreatedOnUtc.Should().BeCloseTo(now.AddHours(-30), TimeSpan.FromSeconds(1));
        tile.TopRows[1].CustomOrderNumber.Should().Be("P-25");
        tile.TopRows[1].OrderTotal.Should().Be(19.5m);
    }

    [Test]
    public async Task PendingPaymentsOrderOldestFirstAndKeepTheTotal()
    {
        var now = DateTime.UtcNow;
        var customer = await AddCustomerAsync("pulse-order-top@example.com", registered: true);
        var ages = new[] { 25, 26, 27, 28, 36, 72 };
        var orders = new List<Order>();
        foreach (var age in ages)
            orders.Add(await AddOrderAsync(customer, StoreA, PaymentStatus.Pending, now.AddHours(-age), age, $"P-age-{age}"));

        await ForgetAsync(StorePulseDefaults.PendingPaymentsTile, StoreA);
        var tile = await _storePulseService.GetPendingPaymentsAsync(StoreA);

        tile.TotalCount.Should().Be(6);
        tile.TopRows.Should().HaveCount(StorePulseDefaults.TopRows);
        tile.TopRows.Select(row => row.CustomOrderNumber).Should().Equal(
            "P-age-72", "P-age-36", "P-age-28", "P-age-27", "P-age-26");
        orders.Should().HaveCount(6);
    }

    [Test]
    public async Task PendingPaymentsFilterByStoreAndCachePerStore()
    {
        var createdOnUtc = DateTime.UtcNow.AddHours(-25);
        var customer = await AddCustomerAsync("pulse-order-store@example.com", registered: true);
        var storeAOrder = await AddOrderAsync(customer, StoreA, PaymentStatus.Pending, createdOnUtc, 10m, "P-A");
        var storeBOrder = await AddOrderAsync(customer, StoreB, PaymentStatus.Pending, createdOnUtc.AddHours(-2), 11m, "P-B");

        await ForgetAsync(StorePulseDefaults.PendingPaymentsTile, StoreA);
        await ForgetAsync(StorePulseDefaults.PendingPaymentsTile, StoreB);
        await ForgetAsync(StorePulseDefaults.PendingPaymentsTile, 0);

        var storeA = await _storePulseService.GetPendingPaymentsAsync(StoreA);
        storeA.TopRows.Select(row => row.OrderId).Should().Equal(storeAOrder.Id);

        await AddOrderAsync(customer, StoreA, PaymentStatus.Pending, createdOnUtc.AddHours(-5), 12m, "P-A2");
        var cached = await _storePulseService.GetPendingPaymentsAsync(StoreA);
        cached.TotalCount.Should().Be(1);

        var storeB = await _storePulseService.GetPendingPaymentsAsync(StoreB);
        storeB.TopRows.Select(row => row.OrderId).Should().Equal(storeBOrder.Id);

        var allStores = await _storePulseService.GetPendingPaymentsAsync(0);
        allStores.TotalCount.Should().Be(3);

        await ForgetAsync(StorePulseDefaults.PendingPaymentsTile, StoreA);
        (await _storePulseService.GetPendingPaymentsAsync(StoreA)).TotalCount.Should().Be(2);
    }

    #endregion

    #region Zero-result searches

    [Test]
    public async Task ZeroResultSearchesCountZeroResultsInsideTheWindow()
    {
        var now = DateTime.UtcNow;
        var customer = await AddCustomerAsync("pulse-search@example.com", registered: true);
        await AddTermAsync(customer, StoreA, "boots", 0, now.AddDays(-1));
        await AddTermAsync(customer, StoreA, "boots", 0, now.AddDays(-2));
        await AddTermAsync(customer, StoreA, "boots", 0, now.AddDays(-3));
        await AddTermAsync(customer, StoreA, "hats", 0, now.AddDays(-6));
        await AddTermAsync(customer, StoreA, "HAT", 0, now.AddDays(-1));
        await AddTermAsync(customer, StoreA, "shirts", null, now.AddDays(-1));
        await AddTermAsync(customer, StoreA, "socks", 4, now.AddDays(-1));
        await AddTermAsync(customer, StoreA, "stale", 0, now.AddDays(-8));
        await AddTermAsync(customer, StoreA, "gone", 0, now.AddDays(-1), deleted: true);
        await AddTermAsync(customer, StoreB, "other", 0, now.AddDays(-1));

        await ForgetAsync(StorePulseDefaults.ZeroResultSearchesTile, StoreA);
        var tile = await _storePulseService.GetZeroResultSearchesAsync(StoreA);

        tile.TotalCount.Should().Be(3);
        tile.TopRows.Select(row => (row.Keyword, row.Count)).Should().Equal(
            ("boots", 3),
            ("HAT", 1),
            ("hats", 1));
    }

    [Test]
    public async Task ZeroResultSearchesOrderByCountAndKeepTheDistinctKeywordTotal()
    {
        var createdOnUtc = DateTime.UtcNow.AddDays(-1);
        var customer = await AddCustomerAsync("pulse-search-top@example.com", registered: true);
        await AddCopiesAsync(customer, "gamma", 3, createdOnUtc);
        await AddCopiesAsync(customer, "alpha", 2, createdOnUtc);
        await AddCopiesAsync(customer, "beta", 2, createdOnUtc);
        for (var count = 1; count <= 4; count++)
            await AddCopiesAsync(customer, $"k{count}", count, createdOnUtc);

        await ForgetAsync(StorePulseDefaults.ZeroResultSearchesTile, StoreA);
        var tile = await _storePulseService.GetZeroResultSearchesAsync(StoreA);

        // gamma 3, alpha 2, beta 2, k4 4, k3 3, k2 2, k1 1 → 7 keywords.
        tile.TotalCount.Should().Be(7);
        tile.TopRows.Should().HaveCount(StorePulseDefaults.TopRows);
        tile.TopRows.Select(row => (row.Keyword, row.Count)).Should().Equal(
            ("k4", 4),
            ("gamma", 3),
            ("k3", 3),
            ("alpha", 2),
            ("beta", 2));
    }

    [Test]
    public async Task ZeroResultSearchesFilterByStoreAndCachePerStore()
    {
        var createdOnUtc = DateTime.UtcNow.AddDays(-1);
        var customer = await AddCustomerAsync("pulse-search-store@example.com", registered: true);
        await AddTermAsync(customer, StoreA, "shared", 0, createdOnUtc);
        await AddTermAsync(customer, StoreB, "shared", 0, createdOnUtc);
        await AddTermAsync(customer, StoreB, "shared", 0, createdOnUtc);

        await ForgetAsync(StorePulseDefaults.ZeroResultSearchesTile, StoreA);
        await ForgetAsync(StorePulseDefaults.ZeroResultSearchesTile, StoreB);
        await ForgetAsync(StorePulseDefaults.ZeroResultSearchesTile, 0);

        var storeA = await _storePulseService.GetZeroResultSearchesAsync(StoreA);
        storeA.TopRows.Should().ContainSingle().Which.Count.Should().Be(1);

        await AddTermAsync(customer, StoreA, "shared", 0, createdOnUtc);
        (await _storePulseService.GetZeroResultSearchesAsync(StoreA)).TopRows.Single().Count.Should().Be(1);

        var storeB = await _storePulseService.GetZeroResultSearchesAsync(StoreB);
        storeB.TopRows.Should().ContainSingle().Which.Count.Should().Be(2);

        var allStores = await _storePulseService.GetZeroResultSearchesAsync(0);
        allStores.TotalCount.Should().Be(1);
        allStores.TopRows.Single().Count.Should().Be(4);

        await ForgetAsync(StorePulseDefaults.ZeroResultSearchesTile, StoreA);
        (await _storePulseService.GetZeroResultSearchesAsync(StoreA)).TopRows.Single().Count.Should().Be(2);
    }

    #endregion

    #region Low stock

    [Test]
    public async Task LowStockMatchesProductAndCombinationQueries()
    {
        await ForgetAsync(StorePulseDefaults.LowStockTile, 0);
        var before = await _storePulseService.GetLowStockAsync();
        var (productsBefore, combinationsBefore) = await LowStockPagesAsync();
        before.TotalCount.Should().Be(productsBefore.TotalCount + combinationsBefore.TotalCount);

        var warehouse = await AddProductAsync(new Product
        {
            Name = "pulse-warehouse",
            ProductType = ProductType.SimpleProduct,
            ManageInventoryMethod = ManageInventoryMethod.ManageStock,
            UseMultipleWarehouses = true,
            StockQuantity = 50,
            MinStockQuantity = -2_000_000_000,
            LowStockActivity = LowStockActivity.DisableBuyButton,
            Published = true,
            LimitedToStores = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await _productService.InsertProductWarehouseInventoryAsync(new ProductWarehouseInventory
        {
            ProductId = warehouse.Id,
            WarehouseId = 1,
            StockQuantity = -2_000_000_000,
            ReservedQuantity = 0
        });

        var unpublished = await AddProductAsync(new Product
        {
            Name = "pulse-unpublished",
            ProductType = ProductType.SimpleProduct,
            ManageInventoryMethod = ManageInventoryMethod.ManageStock,
            StockQuantity = -1_900_000_010,
            MinStockQuantity = -1_900_000_000,
            LowStockActivity = LowStockActivity.Unpublish,
            Published = false,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });

        await AddProductAsync(new Product
        {
            Name = "pulse-healthy",
            ProductType = ProductType.SimpleProduct,
            ManageInventoryMethod = ManageInventoryMethod.ManageStock,
            StockQuantity = 10,
            MinStockQuantity = 1,
            Published = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await AddProductAsync(new Product
        {
            Name = "pulse-deleted",
            ProductType = ProductType.SimpleProduct,
            ManageInventoryMethod = ManageInventoryMethod.ManageStock,
            StockQuantity = 0,
            MinStockQuantity = -2_100_000_000,
            Published = true,
            Deleted = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await AddProductAsync(new Product
        {
            Name = "pulse-grouped",
            ProductType = ProductType.GroupedProduct,
            ManageInventoryMethod = ManageInventoryMethod.ManageStock,
            StockQuantity = 0,
            MinStockQuantity = 1,
            Published = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await AddProductAsync(new Product
        {
            Name = "pulse-untracked",
            ProductType = ProductType.SimpleProduct,
            ManageInventoryMethod = ManageInventoryMethod.DontManageStock,
            StockQuantity = 0,
            MinStockQuantity = 5,
            Published = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });

        var combinationParent = await AddProductAsync(new Product
        {
            Name = "pulse-combination",
            ProductType = ProductType.SimpleProduct,
            ManageInventoryMethod = ManageInventoryMethod.ManageStockByAttributes,
            StockQuantity = 0,
            MinStockQuantity = 0,
            LowStockActivity = LowStockActivity.Nothing,
            Published = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        var lowCombination = new ProductAttributeCombination
        {
            ProductId = combinationParent.Id,
            AttributesXml = string.Empty,
            StockQuantity = 2,
            MinStockQuantity = 9,
            Sku = "pulse-low-combination"
        };
        await _productAttributeService.InsertProductAttributeCombinationAsync(lowCombination);
        await _productAttributeService.InsertProductAttributeCombinationAsync(new ProductAttributeCombination
        {
            ProductId = combinationParent.Id,
            AttributesXml = string.Empty,
            StockQuantity = 8,
            MinStockQuantity = 1,
            Sku = "pulse-healthy-combination"
        });

        var deletedParent = await AddProductAsync(new Product
        {
            Name = "pulse-deleted-parent",
            ProductType = ProductType.SimpleProduct,
            ManageInventoryMethod = ManageInventoryMethod.ManageStockByAttributes,
            Published = true,
            Deleted = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await _productAttributeService.InsertProductAttributeCombinationAsync(new ProductAttributeCombination
        {
            ProductId = deletedParent.Id,
            AttributesXml = string.Empty,
            StockQuantity = 0,
            MinStockQuantity = 5,
            Sku = "pulse-deleted-parent-combination"
        });

        await ForgetAsync(StorePulseDefaults.LowStockTile, 0);
        var tile = await _storePulseService.GetLowStockAsync();
        var (products, combinations) = await LowStockPagesAsync();

        tile.TotalCount.Should().Be(products.TotalCount + combinations.TotalCount);
        tile.TotalCount.Should().Be(before.TotalCount + 3);
        products.Select(product => product.Id).Should().Contain(new[] { warehouse.Id, unpublished.Id });
        products.Select(product => product.Id).Should().NotContain(combinationParent.Id);
        combinations.Select(combination => combination.Id).Should().Contain(lowCombination.Id);

        var publishedOnly = await _productService.GetLowStockProductsAsync(loadPublishedOnly: true, pageSize: int.MaxValue);
        publishedOnly.Select(product => product.Id).Should().NotContain(unpublished.Id);

        var warehouseRow = tile.TopRows.Should().ContainSingle(row => row.ProductId == warehouse.Id && row.CombinationId == null).Subject;
        warehouseRow.Name.Should().Be("pulse-warehouse");
        warehouseRow.StockQuantity.Should().Be(-2_000_000_000);
        warehouseRow.MinStockQuantity.Should().Be(-2_000_000_000);
        warehouseRow.Activity.Should().Be(LowStockActivity.DisableBuyButton);

        var unpublishedRow = tile.TopRows.Should().ContainSingle(row => row.ProductId == unpublished.Id).Subject;
        unpublishedRow.CombinationId.Should().BeNull();
        unpublishedRow.Name.Should().Be("pulse-unpublished");
        unpublishedRow.StockQuantity.Should().Be(-1_900_000_010);
        unpublishedRow.MinStockQuantity.Should().Be(-1_900_000_000);
        unpublishedRow.Activity.Should().Be(LowStockActivity.Unpublish);

        var combinationRow = tile.TopRows.Should().ContainSingle(row => row.CombinationId == lowCombination.Id).Subject;
        combinationRow.ProductId.Should().Be(combinationParent.Id);
        combinationRow.Name.Should().Be("pulse-combination");
        combinationRow.StockQuantity.Should().Be(2);
        combinationRow.MinStockQuantity.Should().Be(9);
        combinationRow.Activity.Should().Be(LowStockActivity.Nothing);

        var combinationIndex = tile.TopRows.ToList().FindIndex(row => row.CombinationId != null);
        combinationIndex.Should().BeGreaterThan(0);
        tile.TopRows.Take(combinationIndex).Should().OnlyContain(row => row.CombinationId == null);
    }

    [Test]
    public async Task LowStockTopRowsFollowTheProductQueryThenCombinations()
    {
        for (var index = 0; index < 6; index++)
        {
            await AddProductAsync(new Product
            {
                Name = $"pulse-low-{index}",
                ProductType = ProductType.SimpleProduct,
                ManageInventoryMethod = ManageInventoryMethod.ManageStock,
                StockQuantity = 0,
                MinStockQuantity = 0,
                Published = true,
                CreatedOnUtc = DateTime.UtcNow,
                UpdatedOnUtc = DateTime.UtcNow
            });
        }

        var parent = await AddProductAsync(new Product
        {
            Name = "pulse-low-combination-parent",
            ProductType = ProductType.SimpleProduct,
            ManageInventoryMethod = ManageInventoryMethod.ManageStockByAttributes,
            Published = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });
        await _productAttributeService.InsertProductAttributeCombinationAsync(new ProductAttributeCombination
        {
            ProductId = parent.Id,
            AttributesXml = string.Empty,
            StockQuantity = 0,
            MinStockQuantity = 1,
            Sku = "pulse-top-combination"
        });

        await ForgetAsync(StorePulseDefaults.LowStockTile, 0);
        var tile = await _storePulseService.GetLowStockAsync();
        var (products, combinations) = await LowStockPagesAsync();
        var expected = products.Select(product => (product.Id, (int?)null))
            .Concat(combinations.Select(combination => (combination.ProductId, (int?)combination.Id)))
            .Take(StorePulseDefaults.TopRows);

        tile.TotalCount.Should().Be(products.TotalCount + combinations.TotalCount);
        tile.TotalCount.Should().BeGreaterThan(StorePulseDefaults.TopRows);
        tile.TopRows.Should().HaveCount(StorePulseDefaults.TopRows);
        tile.TopRows.Select(row => (row.ProductId, row.CombinationId)).Should().Equal(expected);
        tile.TopRows.Should().OnlyContain(row => row.CombinationId == null);
        combinations.Should().NotBeEmpty();
    }

    [Test]
    public async Task LowStockIsCachedUntilTheKeyIsRemoved()
    {
        await ForgetAsync(StorePulseDefaults.LowStockTile, 0);
        var before = await _storePulseService.GetLowStockAsync();

        await AddProductAsync(new Product
        {
            Name = "pulse-cached-low",
            ProductType = ProductType.SimpleProduct,
            ManageInventoryMethod = ManageInventoryMethod.ManageStock,
            StockQuantity = 0,
            MinStockQuantity = 1,
            Published = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        });

        (await _storePulseService.GetLowStockAsync()).TotalCount.Should().Be(before.TotalCount);

        await ForgetAsync(StorePulseDefaults.LowStockTile, 0);
        (await _storePulseService.GetLowStockAsync()).TotalCount.Should().Be(before.TotalCount + 1);
    }

    #endregion

    #region Utilities

    private async Task<(IPagedList<Product> Products, IPagedList<ProductAttributeCombination> Combinations)> LowStockPagesAsync()
    {
        var products = await _productService.GetLowStockProductsAsync(
            vendorId: null, loadPublishedOnly: null, pageSize: StorePulseDefaults.TopRows);
        var combinations = await _productService.GetLowStockProductCombinationsAsync(
            vendorId: null, loadPublishedOnly: null, pageSize: StorePulseDefaults.TopRows);

        return (products, combinations);
    }

    private async Task<Customer> AddCustomerAsync(string email, bool registered)
    {
        var customer = new Customer
        {
            CustomerGuid = Guid.NewGuid(),
            Email = email,
            CreatedOnUtc = DateTime.UtcNow,
            LastActivityDateUtc = DateTime.UtcNow,
            Active = true
        };
        await _customerService.InsertCustomerAsync(customer);
        _customerIds.Add(customer.Id);
        await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping
        {
            CustomerId = customer.Id,
            CustomerRoleId = registered ? _registeredRoleId : _guestRoleId
        });

        return customer;
    }

    private async Task AddCartAsync(Customer customer, int storeId, ShoppingCartType type, int quantity, DateTime updatedOnUtc)
    {
        await _shoppingCartItemRepository.InsertAsync(new ShoppingCartItem
        {
            CustomerId = customer.Id,
            ProductId = _cartProduct.Id,
            StoreId = storeId,
            ShoppingCartType = type,
            Quantity = quantity,
            CreatedOnUtc = updatedOnUtc,
            UpdatedOnUtc = updatedOnUtc
        });
    }

    private async Task<Order> AddOrderAsync(Customer customer, int storeId, PaymentStatus paymentStatus, DateTime createdOnUtc,
        decimal orderTotal, string customOrderNumber, OrderStatus orderStatus = OrderStatus.Pending, bool deleted = false)
    {
        var order = new Order
        {
            OrderGuid = Guid.NewGuid(),
            StoreId = storeId,
            CustomerId = customer.Id,
            BillingAddressId = 1,
            CustomOrderNumber = customOrderNumber,
            OrderTotal = orderTotal,
            PaymentStatus = paymentStatus,
            OrderStatus = orderStatus,
            CreatedOnUtc = createdOnUtc,
            Deleted = deleted
        };
        await _orderRepository.InsertAsync(order);

        return order;
    }

    private async Task AddTermAsync(Customer customer, int storeId, string keyword, int? resultCount, DateTime createdOnUtc, bool deleted = false)
    {
        await _searchTermRepository.InsertAsync(new SearchTerm
        {
            Keyword = keyword,
            CustomerId = customer.Id,
            StoreId = storeId,
            CreatedOnUtc = createdOnUtc,
            ResultCount = resultCount,
            Deleted = deleted
        });
    }

    private async Task AddCopiesAsync(Customer customer, string keyword, int copies, DateTime createdOnUtc)
    {
        for (var index = 0; index < copies; index++)
            await AddTermAsync(customer, StoreA, keyword, 0, createdOnUtc);
    }

    private async Task<Product> AddProductAsync(Product product)
    {
        await _productService.InsertProductAsync(product);
        _productIds.Add(product.Id);

        return product;
    }

    private async Task DeleteTrackedCustomersAsync()
    {
        if (_customerIds.Count == 0)
            return;

        var ids = _customerIds.ToArray();
        await _shoppingCartItemRepository.DeleteAsync(item => ids.Contains(item.CustomerId));
        await _orderRepository.DeleteAsync(order => ids.Contains(order.CustomerId));
        await _searchTermRepository.DeleteAsync(term => ids.Contains(term.CustomerId));
        await _roleMappingRepository.DeleteAsync(mapping => ids.Contains(mapping.CustomerId));
        await _customerRepository.DeleteAsync(customer => ids.Contains(customer.Id));
        _customerIds.Clear();
    }

    private async Task DeleteTrackedProductsAsync()
    {
        if (_productIds.Count == 0)
            return;

        var ids = _productIds.ToArray();
        await _warehouseInventoryRepository.DeleteAsync(row => ids.Contains(row.ProductId));
        await _combinationRepository.DeleteAsync(row => ids.Contains(row.ProductId));
        await _productRepository.DeleteAsync(product => ids.Contains(product.Id));
        _productIds.Clear();
    }

    private Task ForgetAsync(string tile, int storeId)
    {
        return _staticCacheManager.RemoveAsync(StorePulseDefaults.TileCacheKey, tile, storeId);
    }

    private async Task ForgetTilesAsync()
    {
        await ForgetAsync(StorePulseDefaults.AbandonedCartsTile, StoreA);
        await ForgetAsync(StorePulseDefaults.AbandonedCartsTile, StoreB);
        await ForgetAsync(StorePulseDefaults.AbandonedCartsTile, 0);
        await ForgetAsync(StorePulseDefaults.PendingPaymentsTile, StoreA);
        await ForgetAsync(StorePulseDefaults.PendingPaymentsTile, StoreB);
        await ForgetAsync(StorePulseDefaults.PendingPaymentsTile, 0);
        await ForgetAsync(StorePulseDefaults.ZeroResultSearchesTile, StoreA);
        await ForgetAsync(StorePulseDefaults.ZeroResultSearchesTile, StoreB);
        await ForgetAsync(StorePulseDefaults.ZeroResultSearchesTile, 0);
        await ForgetAsync(StorePulseDefaults.LowStockTile, 0);
    }

    #endregion
}
