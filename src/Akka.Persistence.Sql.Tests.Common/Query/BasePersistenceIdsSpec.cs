﻿// -----------------------------------------------------------------------
//  <copyright file="BasePersistenceIdsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.TCK.Query;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.Persistence.Sql.Tests.Common.Query
{
    public abstract class BasePersistenceIdsSpec : PersistenceIdsSpec, IAsyncLifetime
    {
        private readonly ITestConfig _config;
        private readonly TestFixture _fixture;

        public BasePersistenceIdsSpec(ITestConfig config, ITestOutputHelper output, TestFixture fixture)
            : base(Config(config, fixture), nameof(PersistenceIdsSpec), output)
        {
            _config = config;
            _fixture = fixture;
            var persistence = Persistence.Instance.Apply(Sys);
            persistence.JournalFor(null);
        }

        public Task InitializeAsync()
        {
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _fixture.InitializeDbAsync(_config.Database);
        }

        private static Configuration.Config Config(ITestConfig config, TestFixture fixture)
            => ConfigurationFactory.ParseString($@"
                    akka.loglevel = INFO
                    akka.actor{{
                        serializers{{
                            persistence-tck-test=""Akka.Persistence.TCK.Serialization.TestSerializer,Akka.Persistence.TCK""
                        }}
                        serialization-bindings {{
                            ""Akka.Persistence.TCK.Serialization.TestPayload,Akka.Persistence.TCK"" = persistence-tck-test
                        }}
                    }}
                    akka.persistence {{
                        publish-plugin-commands = on
                        journal {{
                            plugin = ""akka.persistence.journal.sql""
                            sql {{
                                provider-name = ""{config.Provider}""
                                tag-write-mode = ""{config.TagMode}""
                                connection-string = ""{fixture.ConnectionString(config.Database)}""
                                auto-initialize = on
                            }}
                        }}
                        snapshot-store {{
                            plugin = ""akka.persistence.snapshot-store.sql""
                            sql {{
                                provider-name = ""{config.Provider}""
                                connection-string = ""{fixture.ConnectionString(config.Database)}""
                                auto-initialize = on
                            }}
                        }}
                    }}
                    akka.persistence.query.journal.sql {{
                        provider-name = ""{config.Provider}""
                        connection-string = ""{fixture.ConnectionString(config.Database)}""
                        tag-read-mode = ""{config.TagMode}""
                        auto-initialize = on
                        refresh-interval = 200ms
                    }}
                    akka.test.single-expect-default = 10s")
                .WithFallback(SqlPersistence.DefaultConfiguration);
    }
}
