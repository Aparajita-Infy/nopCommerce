using FluentMigrator;
using Nop.Core.Domain.Common;
using Nop.Data.Extensions;

namespace Nop.Data.Migrations.UpgradeTo510;

/// <summary>
/// Adds <see cref="SearchTerm.ResultCount"/> so later reports can tell a search that returned
/// nothing from a search recorded before the count was stored.
/// </summary>
[NopSchemaMigration("2026-11-05 00:00:00", "SearchTerm ResultCount for 5.10")]
public class SearchTermResultCountMigration : ForwardOnlyMigration
{
    /// <summary>
    /// Index for the zero-result lookup (store, count, then creation time).
    /// SearchTerm stores one row per storefront search and has no other index.
    /// </summary>
    public const string IndexName = "IX_SearchTerm_StoreId_ResultCount_CreatedOnUtc";

    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        var tableName = nameof(SearchTerm);

        if (!Schema.Table(tableName).Column(nameof(SearchTerm.ResultCount)).Exists())
        {
            this.AddOrAlterColumnFor<SearchTerm>(term => term.ResultCount)
                .AsInt32()
                .Nullable();
        }

        if (!Schema.Table(tableName).Index(IndexName).Exists())
        {
            Create.Index(IndexName)
                .OnTable(tableName)
                .OnColumn(nameof(SearchTerm.StoreId)).Ascending()
                .OnColumn(nameof(SearchTerm.ResultCount)).Ascending()
                .OnColumn(nameof(SearchTerm.CreatedOnUtc)).Ascending()
                .WithOptions().NonClustered();
        }
    }
}
