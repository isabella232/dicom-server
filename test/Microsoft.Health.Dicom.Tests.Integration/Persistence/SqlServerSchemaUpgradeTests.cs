﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Health.Dicom.SqlServer.Features.Schema;
using Microsoft.Health.SqlServer.Features.Schema;
using Microsoft.SqlServer.Dac.Compare;
using Xunit;

namespace Microsoft.Health.Dicom.Tests.Integration.Persistence
{
    public class SqlServerSchemaUpgradeTests
    {
        [Fact]
        public async Task GivenTwoSchemaInitializationMethods_WhenCreatingTwoDatabases_BothSchemasShouldBeEquivalent()
        {
            // Create two databases, one where we apply the the maximum supported version's snapshot SQL schema file
            SqlDataStoreTestsFixture snapshotFixture = new SqlDataStoreTestsFixture(SqlDataStoreTestsFixture.GenerateDatabaseName("SNAPSHOT"));

            // And one where we apply .diff.sql files to upgrade the schema version to the maximum supported version.
            SqlDataStoreTestsFixture diffFixture = new SqlDataStoreTestsFixture(SqlDataStoreTestsFixture.GenerateDatabaseName("DIFF"));

            await snapshotFixture.InitializeAsync(forceIncrementalSchemaUpgrade: false);
            await diffFixture.InitializeAsync(forceIncrementalSchemaUpgrade: true);

            SchemaCompareDatabaseEndpoint snapshotEndpoint = new SchemaCompareDatabaseEndpoint(snapshotFixture.TestConnectionString);
            SchemaCompareDatabaseEndpoint diffEndpoint = new SchemaCompareDatabaseEndpoint(diffFixture.TestConnectionString);
            var comparison = new SchemaComparison(snapshotEndpoint, diffEndpoint);
            SchemaComparisonResult result = comparison.Compare();
            Assert.True(result.IsEqual);

            // cleanup if succeeds
            await snapshotFixture.DisposeAsync();
            await diffFixture.DisposeAsync();
        }

        /// <summary>
        /// There is small window where Sql schema is updated but not populated to web server, so the server still tries to call old stored procedure.
        /// This test validate it works by checking stored procedure compatiblity. 
        /// </summary>
        /// <param name="schemaVersion">New schema version</param>
        [Theory]
        [MemberData(nameof(SchemaDiffVersions))]
        public async Task GivenANewSchemaVersion_WhenApplying_ShouldBackCompatible(int schemaVersion)
        {
            int oldSchemaVersion = schemaVersion - 1;
            // Create Sql store at old schema version
            SqlDataStoreTestsFixture minSqlStore = new SqlDataStoreTestsFixture(SqlDataStoreTestsFixture.GenerateDatabaseName($"COMPATIBLE_{oldSchemaVersion}_"), new SchemaInformation(oldSchemaVersion, oldSchemaVersion));
            await minSqlStore.InitializeAsync(forceIncrementalSchemaUpgrade: false);
            var oldProcedures = await SqlTestUtils.GetStoredProceduresAsync(minSqlStore);

            // Create Sql store at new schema version
            SqlDataStoreTestsFixture newSqlStore = new SqlDataStoreTestsFixture(SqlDataStoreTestsFixture.GenerateDatabaseName($"COMPATIBLE_{schemaVersion}_"), new SchemaInformation(schemaVersion, schemaVersion));
            await newSqlStore.InitializeAsync(forceIncrementalSchemaUpgrade: false);
            var newProcedures = await SqlTestUtils.GetStoredProceduresAsync(newSqlStore);

            // Validate if stored prodcedures are compatible
            StoredProcedureCompatibleValidator.Validate(newProcedures, oldProcedures);

            // Dispose if not exist
            await minSqlStore.DisposeAsync();
            await newSqlStore.DisposeAsync();
        }

        [Theory]
        [MemberData(nameof(SchemaDiffVersions))]
        public async Task GivenASchemaVersion_WhenApplyingDiffTwice_ShouldSucceed(int schemaVersion)
        {
            SqlDataStoreTestsFixture snapshotFixture = new SqlDataStoreTestsFixture(SqlDataStoreTestsFixture.GenerateDatabaseName("SNAPSHOT"));
            snapshotFixture.SchemaInformation = new SchemaInformation(SchemaVersionConstants.Min, schemaVersion - 1);

            await snapshotFixture.InitializeAsync(forceIncrementalSchemaUpgrade: false);
            await snapshotFixture.SchemaUpgradeRunner.ApplySchemaAsync(schemaVersion, applyFullSchemaSnapshot: false, CancellationToken.None);
            await snapshotFixture.SchemaUpgradeRunner.ApplySchemaAsync(schemaVersion, applyFullSchemaSnapshot: false, CancellationToken.None);

            // cleanup if succeeds
            await snapshotFixture.DisposeAsync();
        }

        public static IEnumerable<object[]> SchemaDiffVersions = new List<object[]>(Enumerable.Range(start: SchemaVersionConstants.Min + 1, count: SchemaVersionConstants.Max - SchemaVersionConstants.Min).Select(x => new object[] { x }));
    }
}
