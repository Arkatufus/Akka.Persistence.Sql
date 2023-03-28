﻿// -----------------------------------------------------------------------
//  <copyright file="BaseAllEventsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Query;
using FluentAssertions.Extensions;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Common.Query
{
    public abstract class BaseAllEventsSpec<T> : AllEventsSpec where T : ITestContainer
    {
        private readonly T _fixture;

        protected BaseAllEventsSpec(TagMode tagMode, ITestOutputHelper output, string name, T fixture)
            : base(Config(tagMode, fixture), name, output)
        {
            _fixture = fixture;
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        }

        protected override void AfterAll()
        {
            base.AfterAll();
            Shutdown();
            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }

        private static Configuration.Config Config(TagMode tagMode, T fixture)
            => ConfigurationFactory.ParseString($@"
                    akka.loglevel = INFO
                    akka.persistence.journal.plugin = ""akka.persistence.journal.sql""
                    akka.persistence.journal.sql {{
                        event-adapters {{
                            color-tagger  = ""Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK""
                        }}
                        event-adapter-bindings = {{
                            ""System.String"" = color-tagger
                        }}
                        provider-name = ""{fixture.ProviderName}""
                        tag-write-mode = ""{tagMode}""
                        connection-string = ""{fixture.ConnectionString}""
                        auto-initialize = on
                    }}
                    akka.persistence.query.journal.sql {{
                        provider-name = ""{fixture.ProviderName}""
                        connection-string = ""{fixture.ConnectionString}""
                        tag-read-mode = ""{tagMode}""
                        auto-initialize = on
                        refresh-interval = 1s
                    }}
                    akka.test.single-expect-default = 10s")
                .WithFallback(SqlPersistence.DefaultConfiguration);
    }
}
