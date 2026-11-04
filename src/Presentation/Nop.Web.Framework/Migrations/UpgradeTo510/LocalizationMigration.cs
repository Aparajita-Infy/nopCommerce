using FluentMigrator;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Web.Framework.Extensions;

namespace Nop.Web.Framework.Migrations.UpgradeTo510;

[NopUpdateMigration("2026-10-28 00:00:00", "5.10", UpdateMigrationType.Localization)]
public class LocalizationMigration : MigrationBase
{
    /// <summary>Collect the UP migration expressions</summary>
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        #region Delete locales

        this.DeleteLocaleResources(new List<string>
        {
            
        });

        #endregion

        #region Rename locales

        this.RenameLocales(new Dictionary<string, string>
        {
            
        });

        #endregion

        #region Add or update locales

        this.AddOrUpdateLocaleResource(new Dictionary<string, string>
        {
            //Store Pulse
            ["Admin.Dashboard.StorePulse"] = "Store Pulse",
            ["Admin.Dashboard.StorePulse.AbandonedCarts"] = "Abandoned carts",
            ["Admin.Dashboard.StorePulse.AbandonedCarts.Fields.Email"] = "Email",
            ["Admin.Dashboard.StorePulse.AbandonedCarts.Fields.ItemCount"] = "Total items",
            ["Admin.Dashboard.StorePulse.AbandonedCarts.Fields.LastUpdated"] = "Updated on",
            ["Admin.Dashboard.StorePulse.AbandonedCarts.Hint"] = "Registered customers with a shopping cart last updated more than 24 hours ago and less than 30 days ago.",
            ["Admin.Dashboard.StorePulse.Alert.FailedLoad"] = "Failed to load store pulse.",
            ["Admin.Dashboard.StorePulse.LowStock"] = "Low stock",
            ["Admin.Dashboard.StorePulse.LowStock.Fields.Activity"] = "Low stock activity",
            ["Admin.Dashboard.StorePulse.LowStock.Fields.MinStockQuantity"] = "Minimum stock qty",
            ["Admin.Dashboard.StorePulse.LowStock.Fields.Name"] = "Product name",
            ["Admin.Dashboard.StorePulse.LowStock.Fields.StockQuantity"] = "Stock quantity",
            ["Admin.Dashboard.StorePulse.LowStock.Hint"] = "Products and attribute combinations at or below their minimum stock quantity.",
            ["Admin.Dashboard.StorePulse.NoItems"] = "No items",
            ["Admin.Dashboard.StorePulse.PendingPayments"] = "Pending payments (over 24 hours)",
            ["Admin.Dashboard.StorePulse.PendingPayments.Fields.CreatedOn"] = "Created on",
            ["Admin.Dashboard.StorePulse.PendingPayments.Fields.CustomOrderNumber"] = "Order #",
            ["Admin.Dashboard.StorePulse.PendingPayments.Fields.OrderTotal"] = "Order total",
            ["Admin.Dashboard.StorePulse.PendingPayments.Hint"] = "Orders with payment status Pending that were created more than 24 hours ago.",
            ["Admin.Dashboard.StorePulse.PendingPayments.ViewAllHint"] = "This list also includes pending orders created less than 24 hours ago.",
            ["Admin.Dashboard.StorePulse.ViewAll"] = "View all",
            ["Admin.Dashboard.StorePulse.ZeroResultSearches"] = "Zero-result searches",
            ["Admin.Dashboard.StorePulse.ZeroResultSearches.Fields.Count"] = "Count",
            ["Admin.Dashboard.StorePulse.ZeroResultSearches.Fields.Keyword"] = "Keyword",
            ["Admin.Dashboard.StorePulse.ZeroResultSearches.Hint"] = "Keywords searched in the last 7 days that returned no products."
        });

        #endregion
    }

    /// <summary>Collects the DOWN migration expressions</summary>
    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}