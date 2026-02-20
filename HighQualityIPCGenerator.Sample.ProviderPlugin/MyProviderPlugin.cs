
using Dalamud.IoC;
using Dalamud.Plugin;
using HQIPC.Sample.API;

namespace HQIPC.Sample.ProviderPlugin
{
    // This is not an actual plugin, but it does demonstrate how to provide your plugin's API to other plugins.
    public class MyProviderPlugin : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        // Hang onto the IPC provider registration to dispose when your plugin disposes
        private IDisposable _ipcProviderRegistration;
        private SampleApiImplementation _implementation;

        public MyProviderPlugin()
        {
            // Create whatever hypothetical class you want that implements the IPC interface
            _implementation = new SampleApiImplementation();

            // Call the generated `RegisterIpcProvider` function to make each event and function of your implementation available over Dalamud IPC
            _ipcProviderRegistration = HQIPC.Sample.API.SampleAPI.RegisterIpcProvider(_implementation, PluginInterface);
        }

        public void Dispose()
        {
            // Tell Dalamud to remove the IPC registrations
            _ipcProviderRegistration.Dispose();
        }
    }

    internal class SampleApiImplementation : ISampleAPI
    {
        public event Action<string>? SampleEventOneArg;
        public event Action? SampleEvent;

        public void SampleAction(string first, string second)
        {
            SampleEventOneArg?.Invoke($"{nameof(SampleAction)} called! Both strings smooshed together: {first}{second}");
        }

        public int SampleFunction(int first, int second)
        {
            SampleEvent?.Invoke();

            int sum = first + second;
            return sum;
        }
    }
}
