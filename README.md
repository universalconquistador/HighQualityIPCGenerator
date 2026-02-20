# High Quality IPC Generator

The High Quality IPC Generator is a C# source generator that lets developers create super easy API libraries for exposing their Dalamud plugins via the built-in IPC system.

The input to the generator is an interface (you probably only need one; it can be split into multiple files via `partial` interface definitions), and the output is a class with super easy methods to register an implementation with Dalamud IPC or request an implementation from IPC.


## Usage

### Define your API

In an independent project, declare an interface for your API and give it the `[IpcInterface]` attribute. Here's what the relevant parts of the sample in `HighQualityIPCGenerator.Sample.API` look like:

```csharp
[IpcInterface(ipcNamespace: "HQIPCSample")]
public interface ISampleAPI
{
    event Action<string> SampleEventOneArg;
    event Action SampleEvent;

    int SampleFunction(int first, int second);
    void SampleAction(string first, string second);
}
```

Putting XML documentation on your API interface will let consumers automatically see it when they use your methods and events, so please consider it!


### Provide from the source plugin

From the providing plugin, reference the API project and simply call its generated `RegisterIpcProvider(...)` function, supply the implementation of your interface and the Dalamud plugin interface, and that's it! One line, not counting the eventual `Dispose` when your plugin is shutting down.

Here's what that looks like in the `HighQualityIPCGenerator.Sample.ProviderPlugin` sample:

```csharp
// Call the generated `RegisterIpcProvider` function to make each event and function of your implementation available over Dalamud IPC
IDisposable ipcProviderRegistration = HQIPC.Sample.API.SampleAPI.RegisterIpcProvider(_implementation, PluginInterface);

// ...

// ipcProviderRegistration.Dispose();
```


### Consume from destination plugin(s)

From the consuming plugin, reference the API project and simply call its generated `CreateIpcClient(...)` function, supplying the Dalamud plugin interface, use the returned API implementation, `Dispose` when done, and that's it! Two lines, max.

Here's an example of what that looks like in the `HighQualityIPCGenerator.Sample.ConsumerPlugin` sample:

```csharp
// Create an IPC client which will create Dalamud IPC subscribers lazily as functions are called and events are subscribed to. 
ISampleAPIConsumer consumer = HQIPC.Sample.API.SampleAPI.CreateIpcClient(PluginInterface);

// (Call your API interface's methods on `consumer`...)

// Finally, dispose of any subscriptions that were created on demand
consumer.Dispose();
```


## Compatibility Notes

This generator assumes that IPC names are formatted like `"{id}.{methodName}"`, where `id` is a plugin-specific ID used to namespace the IPCs, and `methodName` is the name of the IPC method or event.

This generator also assumes that you don't want to subscribe to an IPC function or event until you want to call it, and that you don't want to unsubscribe until you call `Dispose`.

Beyond that, HQIPC should be able to be seamlessly swapped in for manual IPC registration on either the provider or consumer side (or both!) without any changes to the other side. In addition, a plugin developer might want to offer both an HQIPC interface as well as their legacy API approach, to give consuming plugins time to adapt; all subscribing plugins should be able to use whichever they please at the same time.

This also means that if, hypothetically, there was a plugin with a vast API surface, a developer could use HQIPC to generate their own consuming code without needing the providing plugin to do anything. I'm not 100% sure you would come out ahead on lines of code this way, as the large boilerplate & redundancy benefits of HQIPC is mainly realized when all providers and consumers use it, but at least you would be typing nice C# functions and events, not a bunch of IPC subscriptions and disposals.


## Human Project

This whole thing is designed and typed by humans, so far just myself (UniversalConquistador), for you, for the joy of it.
