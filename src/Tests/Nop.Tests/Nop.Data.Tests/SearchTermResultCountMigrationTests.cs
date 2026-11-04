using System.Reflection;
using AwesomeAssertions;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Data.Migrations.UpgradeTo500;
using Nop.Data.Migrations.UpgradeTo510;
using UpgradeTo510DataMigration = Nop.Data.Migrations.UpgradeTo510.DataMigration;
using Nop.Services.Customers;
using NUnit.Framework;

namespace Nop.Tests.Nop.Data.Tests;

[TestFixture]
public class SearchTermResultCountMigrationTests : BaseNopTest
{
    [Test]
    public async Task SearchTermResultCountMigrationIsPickedUpAndAddsTheColumn()
    {
        var attribute = typeof(SearchTermResultCountMigration).GetCustomAttribute<NopSchemaMigrationAttribute>();
        attribute.Should().NotBeNull();
        attribute.IsSchemaMigration.Should().BeTrue();
        attribute.TargetMigrationProcess.Should().Be(MigrationProcessType.Update);
        attribute.Version.Should().BeGreaterThan(typeof(SearchTermMigration).GetCustomAttribute<NopSchemaMigrationAttribute>().Version);
        attribute.Version.Should().BeGreaterThan(typeof(UpgradeTo510DataMigration).GetCustomAttribute<NopUpdateMigrationAttribute>().Version);

        var source = GetService<IFilteringMigrationSource>();
        source.GetMigrations(type => type == typeof(SearchTermResultCountMigration)).Should().ContainSingle();

        var dataProvider = GetService<INopDataProvider>();
        var appliedBefore = await LoadVersionAsync(dataProvider, attribute.Version);
        appliedBefore.Should().ContainSingle();
        appliedBefore[0].Description.Should().Be(attribute.Description);

        (await LoadColumnAsync(dataProvider)).Should().ContainSingle()
            .Which.Nullability.Should().Be(0, "installation maps int? to a nullable column");
        (await IndexExistsAsync(dataProvider)).Should().BeTrue("fresh installs create the index from Installation.Indexes");

        var repository = GetService<IRepository<SearchTerm>>();
        var customer = await GetService<ICustomerService>().GetCustomerByEmailAsync(NopTestsDefaults.AdminEmail);
        customer.Should().NotBeNull();
        var termsInserted = false;

        await dataProvider.ExecuteNonQueryAsync($"DROP INDEX IF EXISTS {SearchTermResultCountMigration.IndexName}");
        await dataProvider.ExecuteNonQueryAsync("ALTER TABLE \"SearchTerm\" DROP COLUMN \"ResultCount\"");
        await dataProvider.ExecuteNonQueryAsync($"DELETE FROM \"MigrationVersionInfo\" WHERE \"Version\" = {attribute.Version}");

        try
        {
            (await LoadColumnAsync(dataProvider)).Should().BeEmpty();

            ApplyMigration(source);

            var column = (await LoadColumnAsync(dataProvider)).Should().ContainSingle().Subject;
            column.Nullability.Should().Be(0);
            (await IndexExistsAsync(dataProvider)).Should().BeTrue();

            var appliedAfter = await LoadVersionAsync(dataProvider, attribute.Version);
            appliedAfter.Should().ContainSingle();
            appliedAfter[0].Description.Should().Be(attribute.Description);

            termsInserted = true;
            var unknown = await InsertTermAsync(repository, customer, "migration-unknown-count", null);
            var none = await InsertTermAsync(repository, customer, "migration-zero-count", 0);

            (await repository.GetByIdAsync(unknown.Id)).ResultCount.Should().BeNull();
            (await repository.GetByIdAsync(none.Id)).ResultCount.Should().Be(0);

            await repository.DeleteAsync(term => term.Id == unknown.Id || term.Id == none.Id);
            termsInserted = false;
        }
        finally
        {
            if (termsInserted)
                await repository.DeleteAsync(term => term.Keyword == "migration-unknown-count" || term.Keyword == "migration-zero-count");

            if (!(await LoadColumnAsync(dataProvider)).Any() || !await IndexExistsAsync(dataProvider))
            {
                await dataProvider.ExecuteNonQueryAsync($"DELETE FROM \"MigrationVersionInfo\" WHERE \"Version\" = {attribute.Version}");
                ApplyMigration(source);
            }
        }
    }

    private static void ApplyMigration(IFilteringMigrationSource source)
    {
        var migration = source.GetMigrations(type => type == typeof(SearchTermResultCountMigration)).Single();
        var migrationInfo = GetService<IMigrationRunnerConventions>().GetMigrationInfoForMigration(migration);
        GetService<IMigrationManager>().ApplyUpMigration(migrationInfo);
    }

    private static async Task<SearchTerm> InsertTermAsync(IRepository<SearchTerm> repository, Customer customer, string keyword, int? resultCount)
    {
        var term = new SearchTerm
        {
            Keyword = keyword,
            CustomerId = customer.Id,
            StoreId = 1,
            CreatedOnUtc = DateTime.UtcNow,
            ResultCount = resultCount
        };
        await repository.InsertAsync(term);

        return term;
    }

    private static Task<IList<VersionRow>> LoadVersionAsync(INopDataProvider dataProvider, long version)
    {
        return dataProvider.QueryAsync<VersionRow>(
            $"SELECT \"Version\", \"Description\" FROM \"MigrationVersionInfo\" WHERE \"Version\" = {version}");
    }

    private static Task<IList<ColumnRow>> LoadColumnAsync(INopDataProvider dataProvider)
    {
        return dataProvider.QueryAsync<ColumnRow>(
            "SELECT name AS Name, \"notnull\" AS Nullability FROM pragma_table_info('SearchTerm') WHERE name = 'ResultCount'");
    }

    private static async Task<bool> IndexExistsAsync(INopDataProvider dataProvider)
    {
        var indexes = await dataProvider.QueryAsync<IndexRow>(
            $"SELECT name AS Name FROM pragma_index_list('SearchTerm') WHERE name = '{SearchTermResultCountMigration.IndexName}'");

        return indexes.Count == 1;
    }

    private sealed class VersionRow
    {
        public long Version { get; set; }

        public string Description { get; set; }
    }

    private sealed class ColumnRow
    {
        public string Name { get; set; }

        public long Nullability { get; set; }
    }

    private sealed class IndexRow
    {
        public string Name { get; set; }
    }
}
