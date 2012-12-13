Lifetime
========

A 'Lifetime' is a slightly more flexible System.Threading.CancellationToken.

- 'WhenDead' callback registrations can be removed (by giving them lifetimes that end)
- When a LifetimeSource is garbage collected, its associated lifetime transitions to being immortal (allowing garbage collection of unrun WhenDead callbacks)
- Can be implicitly converted to a CancellationToken

There are also some utility methods (Max/Min/CreateDependentSource) and classes (LifetimeExchanger/DisposableLifetime) provided by the library.

Diagram
========

The library's whole API is simple enough to fit comfortably in a simple diagram:

![API](http://i.imgur.com/Gbh9D.png)
