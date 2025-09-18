using System;

namespace App.Ports
{
    public interface IEventBus
    {
        void Publish<T>(T @event);
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler); // opcional pero útil
    }
}