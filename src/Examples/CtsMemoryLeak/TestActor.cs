// -----------------------------------------------------------------------
//  <copyright file="TestActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Event;
using Akka.Persistence;

namespace CtsMemoryLeak;

public class TestActor: ReceivePersistentActor, IWithTimers
{
    internal sealed class PassivateSelf
    {
        public static readonly PassivateSelf Instance = new PassivateSelf();
        private PassivateSelf() { }
    }
    
    private readonly ILoggingAdapter _log;
    
    public TestActor(string persistenceId)
    {
        PersistenceId = persistenceId;
        _log = Context.GetLogger();
        
        Command<RecoveryCompleted>(_ =>
        {
            Timers.StartSingleTimer("passivate", PassivateSelf.Instance, TimeSpan.FromMinutes(1), Self);
        });
        
        Command<PassivateSelf>(_ => Context.Stop(Self));
        
        CommandAny(msg =>
        {
            _log.Info("Received: {0}", msg.ToString());
        });
    }
    
    public override string PersistenceId { get; }
    public ITimerScheduler Timers { get; set; } = null!;

    protected override void PostStop()
    {
        base.PostStop();
        _log.Info("Passivated: {0}", PersistenceId);
    }
}

