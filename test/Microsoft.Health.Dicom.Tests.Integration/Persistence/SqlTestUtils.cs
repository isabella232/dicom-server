﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using StoredProcedure = Microsoft.SqlServer.Management.Smo.StoredProcedure;

namespace Microsoft.Health.Dicom.Tests.Integration.Persistence
{
    internal static class SqlTestUtils
    {
        /// <summary>
        /// Clear table content asynchronously.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="tableName">Name of table to be cleared.</param>
        /// <returns>The task.</returns>
        public static async Task ClearTableAsync(string connectionString, string tableName)
        {
            EnsureArg.IsNotNullOrWhiteSpace(connectionString, nameof(connectionString));
            EnsureArg.IsNotNullOrWhiteSpace(tableName, nameof(tableName));
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                await sqlConnection.OpenAsync();
                SqlConnection.ClearAllPools();
                using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
                {
                    sqlCommand.CommandText = @$"
                        DELETE
                        FROM {tableName}";

                    await sqlCommand.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Get StoredProcedures in SqlDataaStore
        /// </summary>
        /// <param name="sqlDataStore">The Sql data store</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The stored procedures.</returns>
        public static async Task<System.Collections.Generic.IReadOnlyList<StoredProcedure>> GetStoredProceduresAsync(SqlDataStoreTestsFixture sqlDataStore, CancellationToken cancellationToken = default)
        {
            EnsureArg.IsNotNull(sqlDataStore, nameof(sqlDataStore));
            using var connectionWraper = await sqlDataStore.SqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken);
            ServerConnection connection = new ServerConnection(connectionWraper.SqlConnection);
            Server server = new Server(connection);
            Database db = server.Databases[sqlDataStore.DatabaseName];
            DataTable storedProcedureTable = db.EnumObjects(DatabaseObjectTypes.StoredProcedure);

            List<StoredProcedure> result = new List<StoredProcedure>();
            foreach (DataRow row in storedProcedureTable.Rows)
            {
                string schema = (string)row["Schema"];
                if (schema == "sys" || schema == "INFORMATION_SCHEMA")
                {
                    continue;
                }

                StoredProcedure sp = (StoredProcedure)server.GetSmoObject(new Urn((string)row["Urn"]));
                if (!sp.IsSystemObject)
                {
                    result.Add(sp);
                }
            }
            return result;
        }
    }
}
