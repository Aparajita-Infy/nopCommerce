using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Vendors;
using Nop.Services.Catalog;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.StorePulse;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Home;
using NUnit.Framework;
using ILogger = Nop.Services.Logging.ILogger;

namespace Nop.Tests.Nop.Web.Tests.Admin.Factories;

[TestFixture]
public class HomeModelFactoryStorePulseTests : BaseNopTest
{
    private static readonly TimeSpan UserOffset = TimeSpan.FromHours(3);

    private static readonly string[] EveryTilePermission =
    [
        StandardPermission.Orders.CURRENT_CARTS_MANAGE,
        StandardPermission.Customers.CUSTOMERS_VIEW,
        StandardPermission.Reports.LOW_STOCK,
        StandardPermission.Catalog.PRODUCTS_VIEW,
        StandardPermission.Orders.ORDERS_VIEW
    ];

    [Test]
    public async Task AllPermissions_BuildEachTileFromTheActiveStore()
    {
        var utc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var keyword = "a b&c/d+é";
        var pulse = SamplePulse(utc, keyword);
        pulse.Abandoned.TopRows = Enumerable.Range(1, 6)
            .Select(index => new AbandonedCartRow(index, $"cart{index}@example.com", index, utc))
            .ToList();
        pulse.Abandoned.TotalCount = 11;

        var harness = Create(EveryTilePermission, storeId: 15, pulse: pulse);
        var model = await harness.Factory.PrepareStorePulseModelAsync();

        model.IsLoggedInAsVendor.Should().BeFalse();
        model.HasLoadFailure.Should().BeFalse();

        pulse.AbandonedStoreId.Should().Be(15);
        pulse.PendingStoreId.Should().Be(15);
        pulse.SearchStoreId.Should().Be(15);
        pulse.LowStockCalls.Should().Be(1);

        model.AbandonedCarts.Visible.Should().BeTrue();
        model.AbandonedCarts.TotalCount.Should().Be(11);
        model.AbandonedCarts.Rows.Should().HaveCount(StorePulseDefaults.TopRows);
        model.AbandonedCarts.Rows.Select(row => row.Email).Should().Equal(
            "cart1@example.com", "cart2@example.com", "cart3@example.com", "cart4@example.com", "cart5@example.com");
        model.AbandonedCarts.Rows[0].ItemCount.Should().Be(1);
        model.AbandonedCarts.Rows[0].Url.Should().Be("/Admin/Customer/Edit/1");
        model.AbandonedCarts.Rows[0].LastUpdated.Should().Be(new DateTime(2026, 1, 2, 6, 4, 5));
        model.AbandonedCarts.Rows[0].LastUpdated.Kind.Should().Be(DateTimeKind.Unspecified);
        model.AbandonedCarts.ViewAllUrl.Should().Be("/Admin/ShoppingCart/CurrentCarts");

        model.LowStock.Visible.Should().BeTrue();
        model.LowStock.TotalCount.Should().Be(2);
        model.LowStock.ViewAllUrl.Should().Be("/Admin/Report/LowStock");
        model.LowStock.Rows.Should().HaveCount(2);
        model.LowStock.Rows[0].Name.Should().Be("Plain product");
        model.LowStock.Rows[0].Url.Should().Be("/Admin/Product/Edit/42");
        model.LowStock.Rows[0].Activity.Should().Be("localized:Nothing");
        model.LowStock.Rows[1].Name.Should().Be("Combination parent");
        model.LowStock.Rows[1].Url.Should().Be("/Admin/Product/Edit/42");
        model.LowStock.Rows[1].StockQuantity.Should().Be(0);
        model.LowStock.Rows[1].MinStockQuantity.Should().Be(1);

        model.PendingPayments.Visible.Should().BeTrue();
        model.PendingPayments.TotalCount.Should().Be(1);
        model.PendingPayments.ViewAllUrl.Should().Be($"/Admin/Order/List?paymentStatuses={(int)PaymentStatus.Pending}");
        model.PendingPayments.ViewAllUrl.Should().Be("/Admin/Order/List?paymentStatuses=10");
        model.PendingPayments.Rows.Single().CustomOrderNumber.Should().Be("PULSE-1");
        model.PendingPayments.Rows.Single().OrderTotal.Should().Be("USD 19.95");
        model.PendingPayments.Rows.Single().Url.Should().Be("/Admin/Order/Edit/77");
        model.PendingPayments.Rows.Single().CreatedOn.Should().Be(new DateTime(2026, 1, 2, 6, 4, 5));

        model.ZeroResultSearches.Visible.Should().BeTrue();
        model.ZeroResultSearches.TotalCount.Should().Be(2);
        model.ZeroResultSearches.Rows.Should().HaveCount(2);
        model.ZeroResultSearches.Rows[0].Keyword.Should().Be(keyword);
        model.ZeroResultSearches.Rows[0].Count.Should().Be(4);
        model.ZeroResultSearches.Rows[0].Url.Should().Be("/Admin/Product/List?q=" + Uri.EscapeDataString(keyword));
        model.ZeroResultSearches.Rows[0].Url.Should().NotContain(" ");
        model.ZeroResultSearches.Rows[0].Url.Should().Contain("%20");
        model.ZeroResultSearches.Rows[0].Url.Should().Contain("%26");
        model.ZeroResultSearches.Rows[0].Url.Should().Contain("%2F");
        model.ZeroResultSearches.Rows[0].Url.Should().Contain("%2B");
        model.ZeroResultSearches.Rows[0].Url.Split('?')[1].Should().NotContain("&");
        model.ZeroResultSearches.Rows[1].Keyword.Should().BeNull();
        model.ZeroResultSearches.Rows[1].Url.Should().Be("/Admin/Product/List?q=");
    }

    [Test]
    public async Task AllStoresScope_IsPassedThroughAsZero()
    {
        var harness = Create(EveryTilePermission, storeId: 0, pulse: SamplePulse(DateTime.UtcNow, "none"));

        await harness.Factory.PrepareStorePulseModelAsync();

        harness.Pulse.AbandonedStoreId.Should().Be(0);
        harness.Pulse.PendingStoreId.Should().Be(0);
        harness.Pulse.SearchStoreId.Should().Be(0);
    }

    [Test]
    public async Task MissingPermission_HidesOnlyTheTilesThatNeedIt()
    {
        var noCartManage = await PrepareAsync(Without(StandardPermission.Orders.CURRENT_CARTS_MANAGE));
        noCartManage.Model.AbandonedCarts.Visible.Should().BeFalse();
        noCartManage.Pulse.AbandonedStoreId.Should().BeNull();
        noCartManage.Model.LowStock.Visible.Should().BeTrue();
        noCartManage.Model.PendingPayments.Visible.Should().BeTrue();
        noCartManage.Model.ZeroResultSearches.Visible.Should().BeTrue();

        var noCustomers = await PrepareAsync(Without(StandardPermission.Customers.CUSTOMERS_VIEW));
        noCustomers.Model.AbandonedCarts.Visible.Should().BeFalse();
        noCustomers.Pulse.AbandonedStoreId.Should().BeNull();
        noCustomers.Model.PendingPayments.Visible.Should().BeTrue();

        var noLowStock = await PrepareAsync(Without(StandardPermission.Reports.LOW_STOCK));
        noLowStock.Model.LowStock.Visible.Should().BeFalse();
        noLowStock.Pulse.LowStockCalls.Should().Be(0);
        noLowStock.Model.ZeroResultSearches.Visible.Should().BeTrue();
        noLowStock.Model.AbandonedCarts.Visible.Should().BeTrue();

        var noProducts = await PrepareAsync(Without(StandardPermission.Catalog.PRODUCTS_VIEW));
        noProducts.Model.LowStock.Visible.Should().BeFalse();
        noProducts.Model.ZeroResultSearches.Visible.Should().BeFalse();
        noProducts.Pulse.LowStockCalls.Should().Be(0);
        noProducts.Pulse.SearchStoreId.Should().BeNull();
        noProducts.Model.AbandonedCarts.Visible.Should().BeTrue();
        noProducts.Model.PendingPayments.Visible.Should().BeTrue();

        var noOrders = await PrepareAsync(Without(StandardPermission.Orders.ORDERS_VIEW));
        noOrders.Model.PendingPayments.Visible.Should().BeFalse();
        noOrders.Pulse.PendingStoreId.Should().BeNull();
        noOrders.Model.AbandonedCarts.Visible.Should().BeTrue();
        noOrders.Model.LowStock.Visible.Should().BeTrue();
        noOrders.Model.ZeroResultSearches.Visible.Should().BeTrue();

        var none = await PrepareAsync([]);
        none.Model.AbandonedCarts.Visible.Should().BeFalse();
        none.Model.LowStock.Visible.Should().BeFalse();
        none.Model.PendingPayments.Visible.Should().BeFalse();
        none.Model.ZeroResultSearches.Visible.Should().BeFalse();
        none.Pulse.AbandonedStoreId.Should().BeNull();
        none.Pulse.LowStockCalls.Should().Be(0);
        none.Pulse.PendingStoreId.Should().BeNull();
        none.Pulse.SearchStoreId.Should().BeNull();
    }

    [Test]
    public async Task Vendor_HidesTheCardAndDoesNotQueryTiles()
    {
        var harness = Create(EveryTilePermission, storeId: 4, pulse: SamplePulse(DateTime.UtcNow, "vendor"), vendor: new Vendor { Name = "Pulse vendor" });

        var model = await harness.Factory.PrepareStorePulseModelAsync();

        model.IsLoggedInAsVendor.Should().BeTrue();
        model.AbandonedCarts.Visible.Should().BeFalse();
        model.LowStock.Visible.Should().BeFalse();
        model.PendingPayments.Visible.Should().BeFalse();
        model.ZeroResultSearches.Visible.Should().BeFalse();
        harness.Pulse.AbandonedStoreId.Should().BeNull();
        harness.Pulse.LowStockCalls.Should().Be(0);
        harness.Pulse.PendingStoreId.Should().BeNull();
        harness.Pulse.SearchStoreId.Should().BeNull();
        harness.Store.Verify(context => context.GetActiveStoreScopeConfigurationAsync(), Times.Never);
    }

    [Test]
    public async Task OneTileFailure_KeepsTheOtherTiles()
    {
        var pulse = SamplePulse(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc), "kept");
        pulse.AbandonedException = new InvalidOperationException("cart query failed");
        var harness = Create(EveryTilePermission, storeId: 2, pulse: pulse);

        var model = await harness.Factory.PrepareStorePulseModelAsync();

        model.HasLoadFailure.Should().BeTrue();
        model.AbandonedCarts.Visible.Should().BeTrue();
        model.AbandonedCarts.LoadFailed.Should().BeTrue();
        model.AbandonedCarts.Rows.Should().BeEmpty();
        model.AbandonedCarts.ViewAllUrl.Should().Be("/Admin/ShoppingCart/CurrentCarts");
        model.LowStock.Visible.Should().BeTrue();
        model.LowStock.LoadFailed.Should().BeFalse();
        model.LowStock.Rows.Should().NotBeEmpty();
        model.PendingPayments.LoadFailed.Should().BeFalse();
        model.PendingPayments.Rows.Should().NotBeEmpty();
        model.ZeroResultSearches.LoadFailed.Should().BeFalse();
        model.ZeroResultSearches.Rows[0].Url.Should().Be("/Admin/Product/List?q=kept");
    }

    private static string[] Without(string permission)
    {
        return EveryTilePermission.Where(name => name != permission).ToArray();
    }

    private async Task<(StorePulseModel Model, RecordingStorePulseService Pulse)> PrepareAsync(string[] granted)
    {
        var harness = Create(granted, storeId: 8, pulse: SamplePulse(DateTime.UtcNow, "perm"));
        var model = await harness.Factory.PrepareStorePulseModelAsync();

        return (model, harness.Pulse);
    }

    private static RecordingStorePulseService SamplePulse(DateTime utc, string keyword)
    {
        return new RecordingStorePulseService
        {
            Abandoned = new PulseTile<AbandonedCartRow>(1, new List<AbandonedCartRow>
            {
                new(10, "idle@example.com", 2, utc)
            }),
            LowStock = new PulseTile<LowStockRow>(2, new List<LowStockRow>
            {
                new(42, null, "Plain product", 1, 3, LowStockActivity.Nothing),
                new(42, 99, "Combination parent", 0, 1, LowStockActivity.DisableBuyButton)
            }),
            Pending = new PulseTile<PendingPaymentRow>(1, new List<PendingPaymentRow>
            {
                new(77, "PULSE-1", 19.95m, utc)
            }),
            Searches = new PulseTile<ZeroResultSearchRow>(2, new List<ZeroResultSearchRow>
            {
                new(keyword, 4),
                new(null, 1)
            })
        };
    }

    private Harness Create(string[] granted, int storeId, RecordingStorePulseService pulse, Vendor vendor = null)
    {
        var permissions = new Mock<IPermissionService>();
        var grantedSet = new HashSet<string>(granted);
        permissions.Setup(service => service.AuthorizeAsync(It.IsAny<string>()))
            .ReturnsAsync((string systemName) => grantedSet.Contains(systemName));

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentVendorAsync()).ReturnsAsync(vendor);

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetActiveStoreScopeConfigurationAsync()).ReturnsAsync(storeId);

        var dates = new Mock<IDateTimeHelper>();
        dates.Setup(helper => helper.ConvertToUserTimeAsync(It.IsAny<DateTime>(), It.IsAny<DateTimeKind>()))
            .ReturnsAsync((DateTime value, DateTimeKind _) => value.Add(UserOffset));

        var prices = new Mock<IPriceFormatter>();
        prices.Setup(formatter => formatter.FormatPriceAsync(It.IsAny<decimal>(), true, false))
            .ReturnsAsync((decimal price, bool _, bool _) => $"USD {price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");

        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetLocalizedEnumAsync(It.IsAny<LowStockActivity>(), It.IsAny<int?>()))
            .ReturnsAsync((LowStockActivity activity, int? _) => $"localized:{activity}");

        var logger = new Mock<ILogger>();
        logger.Setup(item => item.ErrorAsync(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Customer>()))
            .Returns(Task.CompletedTask);

        // The pulse method does not use the dashboard report dependencies. Passing them would pull
        // admin factories that the test host does not register.
        var factory = new HomeModelFactory(
            null,
            null,
            dates.Object,
            localization.Object,
            logger.Object,
            null,
            permissions.Object,
            prices.Object,
            null,
            null,
            storeContext.Object,
            pulse,
            workContext.Object,
            null);

        return new Harness(factory, pulse, storeContext);
    }

    private sealed record Harness(HomeModelFactory Factory, RecordingStorePulseService Pulse, Mock<IStoreContext> Store);

    private sealed class RecordingStorePulseService : IStorePulseService
    {
        public PulseTile<AbandonedCartRow> Abandoned { get; set; } = new();

        public PulseTile<LowStockRow> LowStock { get; set; } = new();

        public PulseTile<PendingPaymentRow> Pending { get; set; } = new();

        public PulseTile<ZeroResultSearchRow> Searches { get; set; } = new();

        public Exception AbandonedException { get; set; }

        public int? AbandonedStoreId { get; private set; }

        public int? PendingStoreId { get; private set; }

        public int? SearchStoreId { get; private set; }

        public int LowStockCalls { get; private set; }

        public Task<PulseTile<AbandonedCartRow>> GetAbandonedCartsAsync(int storeId)
        {
            AbandonedStoreId = storeId;
            if (AbandonedException != null)
                throw AbandonedException;

            return Task.FromResult(Abandoned);
        }

        public Task<PulseTile<LowStockRow>> GetLowStockAsync()
        {
            LowStockCalls++;

            return Task.FromResult(LowStock);
        }

        public Task<PulseTile<PendingPaymentRow>> GetPendingPaymentsAsync(int storeId)
        {
            PendingStoreId = storeId;

            return Task.FromResult(Pending);
        }

        public Task<PulseTile<ZeroResultSearchRow>> GetZeroResultSearchesAsync(int storeId)
        {
            SearchStoreId = storeId;

            return Task.FromResult(Searches);
        }
    }
}
