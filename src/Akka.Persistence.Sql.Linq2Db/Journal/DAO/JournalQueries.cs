// -----------------------------------------------------------------------
//  <copyright file="JournalQueries.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#if USE_COMPILED_QUERIES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using LanguageExt;
using LinqToDB;
using LinqToDB.Data;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Dao;

public sealed class JournalQueries
{
    private readonly JournalConfig _config;

    public readonly Func<DataConnection, Seq<JournalRow>, Task<BulkCopyRowsCopied>> InsertMultipleDefault;
    public readonly Func<DataConnection, Seq<JournalRow>, Task<BulkCopyRowsCopied>> InsertMultipleMultiple;
    public readonly Func<DataConnection, string, long, Task<int>> MarkJournalMessagesAsDeleted;
    public readonly Func<DataConnection, string, Task<long>> MaxMarkedForDeletionMaxPersistenceId;
    public readonly Func<DataConnection, string, long, Task<int>> InsertOrUpdateMetadata;
    public readonly Func<DataConnection, string, long, Task<int>> Delete;
    public readonly Func<DataConnection, string, long, byte[], Task<int>> Update;
    public readonly Func<DataConnection, string, long, long, Task<List<JournalRow>>> Select;
    public readonly Func<DataConnection, string, long, long, int, Task<List<JournalRow>>> SelectWithLimit;

    public readonly Func<DataConnection, string, long, Task<long?>> MaxSeqForPersistenceIdWithMinIdCompatibilityMode;
    public readonly Func<DataConnection, string, long, Task<long?>> MaxSeqForPersistenceIdWithMinId;
    public readonly Func<DataConnection, string, Task<long?>> MaxSeqForPersistenceIdCompatibilityMode;
    public readonly Func<DataConnection, string, Task<long?>> MaxSeqForPersistenceId;

    public JournalQueries(JournalConfig config)
    {
        _config = config;
        
        InsertMultipleDefault = CompiledQuery.Compile<DataConnection, Seq<JournalRow>, Task<BulkCopyRowsCopied>>(
            (dc, seq) => dc.GetTable<JournalRow>()
                .BulkCopyAsync(
                    new BulkCopyOptions
                    {
                        BulkCopyType = BulkCopyType.Default,
                        UseParameters = _config.DaoConfig.PreferParametersOnMultiRowInsert,
                        MaxBatchSize = _config.DaoConfig.DbRoundTripBatchSize
                    }, seq, CancellationToken.None));
        
        InsertMultipleMultiple = CompiledQuery.Compile<DataConnection, Seq<JournalRow>, Task<BulkCopyRowsCopied>>(
            (dc, seq) => dc.GetTable<JournalRow>()
                .BulkCopyAsync(
                    new BulkCopyOptions
                    {
                        BulkCopyType = BulkCopyType.MultipleRows,
                        UseParameters = _config.DaoConfig.PreferParametersOnMultiRowInsert,
                        MaxBatchSize = _config.DaoConfig.DbRoundTripBatchSize
                    }, seq, CancellationToken.None));

        MarkJournalMessagesAsDeleted = CompiledQuery.Compile<DataConnection, string, long, Task<int>>(
            (dc, persistenceId, maxSequenceNr) => dc.GetTable<JournalRow>()
                .Where(r => r.PersistenceId == persistenceId && r.SequenceNumber <= maxSequenceNr)
                .Set(r => r.Deleted, true)
                .UpdateAsync(CancellationToken.None));

        MaxMarkedForDeletionMaxPersistenceId = CompiledQuery.Compile<DataConnection, string, Task<long>>(
            (dc, persistenceId) => dc.GetTable<JournalRow>()
                .Where(r => r.PersistenceId == persistenceId && r.Deleted)
                .OrderByDescending(r => r.SequenceNumber)
                .Select(r => r.SequenceNumber)
                .Take(1)
                .FirstOrDefaultAsync(CancellationToken.None));

        InsertOrUpdateMetadata = CompiledQuery.Compile<DataConnection, string, long, Task<int>>(
            (dc, persistenceId, maxMarkedDeletion) => dc.GetTable<JournalMetaData>()
                .InsertOrUpdateAsync(
                    () => new JournalMetaData
                    {
                        PersistenceId = persistenceId,
                        SequenceNumber = maxMarkedDeletion
                    },
                    jmd => new JournalMetaData(),
                    () => new JournalMetaData
                    {
                        PersistenceId = persistenceId,
                        SequenceNumber = maxMarkedDeletion
                    }, CancellationToken.None));

        Delete = CompiledQuery.Compile<DataConnection, string, long, Task<int>>(
            (dc, persistenceId, toSequenceNr) => dc.GetTable<JournalRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber <= toSequenceNr)
                .DeleteAsync(CancellationToken.None));

        MaxSeqForPersistenceIdCompatibilityMode = CompiledQuery.Compile<DataConnection, string, Task<long?>>(
            (dc, persistenceId) => dc.GetTable<JournalRow>()
                .Where(r => r.PersistenceId == persistenceId)
                .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue())
                .Union(dc
                    .GetTable<JournalMetaData>()
                    .Where(r => r.PersistenceId == persistenceId)
                    .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue()))
                .MaxAsync(CancellationToken.None));
        
        MaxSeqForPersistenceId = CompiledQuery.Compile<DataConnection, string, Task<long?>>(
            (dc, persistenceId) => dc.GetTable<JournalRow>()
                .Where(r => r.PersistenceId == persistenceId)
                .Select(r => (long?)r.SequenceNumber)
                .MaxAsync(CancellationToken.None));

        MaxSeqForPersistenceIdWithMinIdCompatibilityMode =
            CompiledQuery.Compile<DataConnection, string, long, Task<long?>>(
                (dc, persistenceId, minSequenceNumber) => dc.GetTable<JournalRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber > minSequenceNumber)
                    .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue())
                    .Union(dc
                        .GetTable<JournalMetaData>()
                        .Where(r =>
                            r.SequenceNumber > minSequenceNumber &&
                            r.PersistenceId == persistenceId)
                        .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue()))
                    .MaxAsync(CancellationToken.None));
        
        MaxSeqForPersistenceIdWithMinId = CompiledQuery.Compile<DataConnection, string, long, Task<long?>>(
            (dc, persistenceId, minSequenceNumber) => dc.GetTable<JournalRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber > minSequenceNumber)
                .Select(r => (long?)r.SequenceNumber)
                .MaxAsync(CancellationToken.None));

        Update = CompiledQuery.Compile<DataConnection, string, long, byte[], Task<int>>(
            (dc, persistenceId, sequenceNr, payload) => dc.GetTable<JournalRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber == sequenceNr)
                .Set(r => r.Message, payload)
                .UpdateAsync(CancellationToken.None));

        Select = CompiledQuery.Compile<DataConnection, string, long, long, Task<List<JournalRow>>>(
            (dc, persistenceId, from, to) => dc.GetTable<JournalRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber >= from &&
                    r.SequenceNumber <= to &&
                    r.Deleted == false)
                .OrderBy(r => r.SequenceNumber)
                .ToListAsync(CancellationToken.None));
        
        SelectWithLimit = CompiledQuery.Compile<DataConnection, string, long, long, int, Task<List<JournalRow>>>(
            (dc, persistenceId, from, to, limit) => dc.GetTable<JournalRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber >= from &&
                    r.SequenceNumber <= to &&
                    r.Deleted == false)
                .OrderBy(r => r.SequenceNumber)
                .Take(limit)
                .ToListAsync(CancellationToken.None));
    }
}
#endif