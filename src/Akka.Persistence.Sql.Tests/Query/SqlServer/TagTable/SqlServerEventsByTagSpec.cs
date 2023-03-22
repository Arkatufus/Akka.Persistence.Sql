﻿// -----------------------------------------------------------------------
//  <copyright file="SqlServerEventsByTagSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Query;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.SqlServer.TagTable
{
    [Collection("PersistenceSpec")]
    public class SqlServerEventsByTagSpec : BaseEventsByTagSpec
    {
        public SqlServerEventsByTagSpec(ITestOutputHelper output, TestFixture fixture)
            : base(SqlServerConfig.TagTable, output, fixture) { }
    }
}
