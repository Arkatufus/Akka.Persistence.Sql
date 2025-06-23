// -----------------------------------------------------------------------
//  <copyright file="TestActorSupervisor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;

namespace CtsMemoryLeak;

public class TestActorSupervisor: ReceiveActor
{
    public sealed record TestMessage(string EntityId);
    
    public TestActorSupervisor()
    {
        Receive<TestMessage>(msg =>
        {
            var child = Context.Child(msg.EntityId);
            if (child.Equals(ActorRefs.Nobody))
                child = Context.ActorOf(Props.Create(() => new TestActor(msg.EntityId)), msg.EntityId);
            
            child.Tell(msg.EntityId);
        });
    }
}
