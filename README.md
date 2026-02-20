# High Quality IPC Generator

[![NuGet Badge](https://img.shields.io/nuget/v/HQIPC)](https://www.nuget.org/packages/HQIPC/)

The High Quality IPC Generator is a C# source generator that lets developers create super easy API libraries for exposing their Dalamud FFXIV plugins via the built-in IPC system. It only handles automating the IPC registration boilerplate without prescribing anything beyond that, leaving you in complete control of your API.

HQIPC lets you just write a regular C# `interface` for your API and add one single attribute. Then it takes care of generating the boilerplate for providing and consuming it via IPC! This has a number of advantages over other Dalamud IPC approaches:
 - Only declare your API in one place! A single source of truth makes updating your IPC methods trivial without unnecessary toil, and ensures you don't accidentally mismatch anything.
 - XML documentation for your users! Because your API is only declared in one place, XML documentation attached to its events and methods are easily visible when authoring the providing plugin and the consuming plugins.
 - No manually specifying string IDs for IPC events and methods! A member's symbol name is the single source of truth for its IPC name.
 - No per-method IPC boilerplate like individual attributes or classes.
 - Consumers don't need to manually subscribe to individual events and methods. Consuming your API becomes a single line of joy!
 - Lightweight and super simple! Basically zero overhead with no reflection.

The input to the generator is an interface (it can be split into multiple files via `partial` interface definitions), and the output is a class with two super straightforward `static` methods to provide or consume the API via IPC.

For example, HQIPC would take an interface, like this:
```csharp
[IpcInterface("DemoIPC")]
interface IDemo { ... }
```
and produce two static functions that take care of all the Dalamud IPC concerns, like this:
```csharp
class Demo
{
    static IDisposable RegisterIpcProvider(IDemo implementation, IDalamudPluginInterface pluginInterface) { ... }
    static IDemo CreateIpcClient(IDalamudPluginInterface pluginInterface) { ... }
}
```


## Usage

### 1) Define your API

In an independent project, first add a NuGet reference to `HQAPI`.
Then, declare an interface for your API and give it the `[IpcInterface]` attribute, including a string to namespace all the IPC identifiers with. Here's what the relevant parts of the sample in `HighQualityIPCGenerator.Sample.API` look like:

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


### 2) Provide from the source plugin

From the providing plugin, reference the API project and simply call its generated `RegisterIpcProvider(...)` function, supply the implementation of your interface and the Dalamud plugin interface, and that's it! One line, not counting the eventual `Dispose` when your plugin is shutting down.

Here's what that looks like in the `HighQualityIPCGenerator.Sample.ProviderPlugin` sample:

```csharp
// Call the generated `RegisterIpcProvider` function to make each event and function of your implementation available over Dalamud IPC
IDisposable ipcProviderRegistration = HQIPC.Sample.API.SampleAPI.RegisterIpcProvider(_implementation, PluginInterface);

// (...)

// Dispose the registration to unregister your events and methods from Dalamud IPC
ipcProviderRegistration.Dispose();
```


### 3) Consume from destination plugin(s)

From the consuming plugin, reference the API project and simply call its generated `CreateIpcClient(...)` function, supplying the plugin interface given by Dalamud. Then use the returned interface to invoke the API via IPC, `Dispose` it when done, and that's it! Two lines, max.

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
