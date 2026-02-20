using Dalamud.IoC;
using Dalamud.Plugin;
using HQIPC.Sample.API;

namespace HQIPC.Sample.ConsumerPlugin
{
    // This is not an actual plugin, but it does demonstrate how to consume the API of another plugin via Dalamud IPC.
    public class MyConsumerPlugin : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        // The type here is a generated interface that extends the authored interface by adding `IDisposable`
        private ISampleAPIConsumer _consumer;

        public MyConsumerPlugin()
        {
            // Create an IPC client which will create Dalamud IPC subscribers lazily as functions are called and events are subscribed to. 
            _consumer = HQIPC.Sample.API.SampleAPI.CreateIpcClient(PluginInterface);

            // Call whatever API functions and events you like!
            _consumer.SampleEvent += () => Console.WriteLine("SampleEvent raised!");
            _consumer.SampleEventOneArg += message => Console.WriteLine($"SampleEventOneArg raised with argument {message}!");

            _consumer.SampleAction("Hello ", "World!");
            var result = _consumer.SampleFunction(68, 1);
        }

        public void Dispose()
        {
            // Dispose any event listeners that were created on demand
            _consumer.Dispose();
        }
    }
}
