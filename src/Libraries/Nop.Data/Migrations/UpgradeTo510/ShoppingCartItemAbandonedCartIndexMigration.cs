using FluentMigrator;
using Nop.Core.Domain.Orders;

namespace Nop.Data.Migrations.UpgradeTo510;

/// <summary>
/// Index for the abandoned-cart tile. The query filters a store's shopping carts, groups by customer,
/// and aggregates the latest update and the quantity.
/// </summary>
/// <remarks>
/// The install index <c>IX_ShoppingCartItem_ShoppingCartTypeId_CustomerId</c> has no store column, so a
/// store-scoped scan still reads every store. <c>UpdatedOnUtc</c> and <c>Quantity</c> are not in that index.
/// The window is applied to MAX(UpdatedOnUtc), so the date cannot lead the index; it follows the customer
/// grouping key. Quantity is last so the aggregate is covered without a row lookup.
/// </remarks>
[NopSchemaMigration("2026-11-05 00:01:00", "ShoppingCartItem abandoned-cart index for 5.10")]
public class ShoppingCartItemAbandonedCartIndexMigration : ForwardOnlyMigration
{
    /// <summary>
    /// Short name shared by the install and upgrade paths.
    /// MySQL rejects identifiers longer than 64 characters, and PostgreSQL truncates them at 63,
    /// so the column list stays in the index key and out of the name.
    /// </summary>
    public const string IndexName = "IX_ShoppingCartItem_AbandonedCart";

    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        var tableName = nameof(ShoppingCartItem);

        if (!Schema.Table(tableName).Index(IndexName).Exists())
        {
            Create.Index(IndexName)
                .OnTable(tableName)
                .OnColumn(nameof(ShoppingCartItem.StoreId)).Ascending()
                .OnColumn(nameof(ShoppingCartItem.ShoppingCartTypeId)).Ascending()
                .OnColumn(nameof(ShoppingCartItem.CustomerId)).Ascending()
                .OnColumn(nameof(ShoppingCartItem.UpdatedOnUtc)).Ascending()
                .OnColumn(nameof(ShoppingCartItem.Quantity)).Ascending()
                .WithOptions().NonClustered();
        }
    }
}
