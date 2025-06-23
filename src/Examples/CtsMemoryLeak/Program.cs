using Akka.Actor;
using Akka.Configuration;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using CtsMemoryLeak;
using JetBrains.Profiler.Api;
using JetBrains.Profiler.SelfApi;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var container = new Testcontainers.PostgreSql.PostgreSqlBuilder().Build();
await container.StartAsync();

var connectionString = container.GetConnectionString();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logger =>
    {
        logger.ClearProviders();
        logger.AddConsole();
        logger.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddAkka("akkacluster", (builder, sp) =>
        {
	        builder
		        .ConfigureLoggers(setup =>
		        {
			        setup.LogLevel = Akka.Event.LogLevel.InfoLevel;
			        setup.LogConfigOnStart = false;
			        setup.ClearLoggers();
                    setup.AddLoggerFactory();
                })
		        .WithSqlPersistence(
		         connectionString: connectionString,
		         providerName: ProviderName.PostgreSQL,
		         databaseMapping: DatabaseMapping.PostgreSql,
		         tagStorageMode: TagMode.TagTable,
		         useWriterUuidColumn: true,
		         autoInitialize: true)
                .WithActors((system, registry) =>
                {
                    var actor = system.ActorOf(Props.Create(() => new TestActorSupervisor()), "supervisor");
                    registry.Register<TestActorSupervisor>(actor);
                });
        });
    })
    .UseConsoleLifetime()
    .Build();

await host.StartAsync();

var registry = host.Services.GetRequiredService<IActorRegistry>();
var supervisor = await registry.GetAsync<TestActorSupervisor>();

// setup
await DotMemory.InitAsync();
var config = new DotMemory.Config()
    .SaveToFile("F:\\Trace\\CtsMemoryLeak", true);

DotMemory.Attach(config);

// warm up
Console.WriteLine("Warming up persistence");
foreach (var seq in Enumerable.Range(0, 10))
{
    supervisor.Tell(new TestActorSupervisor.TestMessage($"warmup-{seq}"));
}
await Task.Delay(TimeSpan.FromSeconds(3));

Console.WriteLine("Starting test");

var lastActorId = 0L;
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));

GC.Collect();
DotMemory.GetSnapshot();
try
{
    while (!cts.IsCancellationRequested)
    {
        await timer.WaitForNextTickAsync(cts.Token);
        lastActorId++;
        supervisor.Tell(new TestActorSupervisor.TestMessage($"e-{lastActorId}"));
    }
}
catch (OperationCanceledException)
{
    // no-op
}

GC.Collect();
DotMemory.GetSnapshot();

// clean-up
DotMemory.Detach();
await host.StopAsync();
Console.WriteLine("Test complete");
