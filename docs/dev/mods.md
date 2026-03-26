## Load Order

When defining new messages, you are required to annotate them with attributes from the `multiplayer` assemblies. The `.NET` runtime (specifically the CLR) resolves attribute types during metadata loading - **before** your assembly is actually loaded. Because of this, your code never gets a chance to run and hook into assembly resolution.

As a result, the multiplayer assemblies must already be loaded. Otherwise, a `TypeLoadException` will occur.

Unfortunately, UnityModManager does not provide explicit control over load order, so the only reliable workaround is **folder naming**.

---
### Disclaimer: any contracts/api is not final and may change without further notice.
---

## Accessing WOTRMultiplayer objects

The simplest way to access multiplayer instances is through `ServiceProvider`. While this pattern is generally discouraged in typical application design, it works well here because it allows direct access without worrying about structure or encapsulation.

You can access it via:
```
WOTRMultiplayer.Main.ServiceProvider
```

Keep in mind:

* Only **singleton** services are safe to use this way.
* Requesting non-singleton services will return a new instance that is not actually used by the mod.

*In extreme cases, you can always use reflection to access or modify whatever you need.*

## Deterministic [Random](https://en.wikipedia.org/wiki/Pseudorandom_number_generator)

All random values must be consistent across all players to avoid desynchronizing the game state. The multiplayer mod provides a way to generate deterministic values without requiring additional network messages.

`IMultiplayerActorAccessor` provides access to `MultiplayerClient` and `MultiplayerServer`, which hold the multiplayer state.

Use the `GetSeededContext` method to retrieve information about the current deterministic context. The `Id` property contains the final value that should be included in the identifier when generating random values.

The `SeedKind` parameter allows you to exclude specific seeds from the calculation.

For example, the following excludes `AreaSeed` (generated on each area load) from the resulting value:

```csharp
MultiplayerClient.GetSeededContext(SeedKind.All & ~SeedKind.AreaSeed);
```

Basic example of generating a deterministic random number between `0` and `100`:

```csharp
var currentActor = Main.ServiceProvider.GetService<IMultiplayerActorAccessor>().Current;
var seededContext = currentActor.GetSeededContext();
var identifier = $"MyActionName_{seededContext.Id}";
var deterministicValue = Main.Multiplayer.ValueGenerator.Range(
    seededContext.Lifetime,
    identifier,
    minInclusive: 0,
    maxExclusive: 100
);
```

`ValueGenerator` produces values based on two parameters: `Lifetime` and `Identifier`.

* **Lifetime** - determines when the random sequence resets (for example, *per combat turn* or *per combat*).
* **Identifier** - the RNG seed used to generate the value.

You can override the lifetime if necessary, but in most cases it should be taken directly from `SeededContext`.


## Network Messages

In most cases, you will work with `INetworkServer` or `INetworkClient`. These provide direct access to network messages, allowing you to listen to existing messages or send custom ones.

When registering a handler, a simple priority system is used. You can specify whether your handler should run before or after others, including those already registered by the multiplayer mod.

Use **High** priority only when your handler must execute before the default handlers.
