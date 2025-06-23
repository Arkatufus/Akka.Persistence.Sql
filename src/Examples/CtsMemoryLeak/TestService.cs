// -----------------------------------------------------------------------
//  <copyright file="TestService.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Hosting;

namespace CtsMemoryLeak;

public class TestService(IRequiredActor<TestActorSupervisor> reqSupervisor): IHostedService
{
    private readonly CancellationTokenSource _cts = new ();
    private Task? _runningTask;
    private long _lastActorId;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _runningTask = StartTimerJob(_cts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync();
        if(_runningTask is not null)
            await _runningTask;
            
        _cts.Dispose();
    }

    private async Task StartTimerJob(CancellationToken ct)
    {
        var supervisor = await reqSupervisor.GetAsync(ct);
        var periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                await periodicTimer.WaitForNextTickAsync(ct);
                _lastActorId++;
                supervisor.Tell(new TestActorSupervisor.TestMessage($"e-{_lastActorId}"));
            }
        }
        catch (OperationCanceledException)
        {
            // no-op
        }
    }
}
