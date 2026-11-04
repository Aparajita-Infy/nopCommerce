using System.Reflection;
using AwesomeAssertions;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Data.Migrations.UpgradeTo510;
using NUnit.Framework;

namespace Nop.Tests.Nop.Data.Tests;

[TestFixture]
public class ShoppingCartItemAbandonedCartIndexMigrationTests : BaseNopTest
{
    [Test]
    public async Task AbandonedCartIndexIsCreatedOnInstallAndByTheUpgradeMigration()
    {
        var attribute = typeof(ShoppingCartItemAbandonedCartIndexMigration).GetCustomAttribute<NopSchemaMigrationAttribute>();
        attribute.Should().NotBeNull();
        attribute.IsSchemaMigration.Should().BeTrue();
        attribute.TargetMigrationProcess.Should().Be(MigrationProcessType.Update);
        attribute.Version.Should().BeGreaterThan(typeof(SearchTermResultCountMigration).GetCustomAttribute<NopSchemaMigrationAttribute>().Version);

        var source = GetService<IFilteringMigrationSource>();
        source.GetMigrations(type => type == typeof(ShoppingCartItemAbandonedCartIndexMigration)).Should().ContainSingle();

        var dataProvider = GetService<INopDataProvider>();
        var appliedBefore = await LoadVersionAsync(dataProvider, attribute.Version);
        appliedBefore.Should().ContainSingle();
        appliedBefore[0].Description.Should().Be(attribute.Description);

        (await LoadColumnsAsync(dataProvider)).Select(column => column.Name).Should().Equal(
            new[]
            {
                nameof(ShoppingCartItem.StoreId),
                nameof(ShoppingCartItem.ShoppingCartTypeId),
                nameof(ShoppingCartItem.CustomerId),
                nameof(ShoppingCartItem.UpdatedOnUtc),
                nameof(ShoppingCartItem.Quantity)
            },
            "fresh installs create the index from Installation.Indexes");

        await dataProvider.ExecuteNonQueryAsync($"DROP INDEX IF EXISTS {ShoppingCartItemAbandonedCartIndexMigration.IndexName}");
        await dataProvider.ExecuteNonQueryAsync($"DELETE FROM \"MigrationVersionInfo\" WHERE \"Version\" = {attribute.Version}");

        try
        {
            (await LoadColumnsAsync(dataProvider)).Should().BeEmpty();

            ApplyMigration(source);

            (await LoadColumnsAsync(dataProvider)).Select(column => column.Name).Should().Equal(
                nameof(ShoppingCartItem.StoreId),
                nameof(ShoppingCartItem.ShoppingCartTypeId),
                nameof(ShoppingCartItem.CustomerId),
                nameof(ShoppingCartItem.UpdatedOnUtc),
                nameof(ShoppingCartItem.Quantity));

            var appliedAfter = await LoadVersionAsync(dataProvider, attribute.Version);
            appliedAfter.Should().ContainSingle();
            appliedAfter[0].Description.Should().Be(attribute.Description);
        }
        finally
        {
            if (!(await LoadColumnsAsync(dataProvider)).Any())
            {
                await dataProvider.ExecuteNonQueryAsync($"DELETE FROM \"MigrationVersionInfo\" WHERE \"Version\" = {attribute.Version}");
                ApplyMigration(source);
            }
        }
    }

    private static void ApplyMigration(IFilteringMigrationSource source)
    {
        var migration = source.GetMigrations(type => type == typeof(ShoppingCartItemAbandonedCartIndexMigration)).Single();
        var migrationInfo = GetService<IMigrationRunnerConventions>().GetMigrationInfoForMigration(migration);
        GetService<IMigrationManager>().ApplyUpMigration(migrationInfo);
    }

    private static Task<IList<VersionRow>> LoadVersionAsync(INopDataProvider dataProvider, long version)
    {
        return dataProvider.QueryAsync<VersionRow>(
            $"SELECT \"Version\", \"Description\" FROM \"MigrationVersionInfo\" WHERE \"Version\" = {version}");
    }

    private static Task<IList<IndexColumnRow>> LoadColumnsAsync(INopDataProvider dataProvider)
    {
        return dataProvider.QueryAsync<IndexColumnRow>(
            "SELECT name AS Name, seqno AS Position FROM pragma_index_info('" +
            ShoppingCartItemAbandonedCartIndexMigration.IndexName + "') ORDER BY seqno");
    }

    private sealed class VersionRow
    {
        public long Version { get; set; }

        public string Description { get; set; }
    }

    private sealed class IndexColumnRow
    {
        public string Name { get; set; }

        public long Position { get; set; }
    }
}
