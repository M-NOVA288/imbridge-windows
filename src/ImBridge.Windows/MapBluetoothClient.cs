using ImBridge.Core;
using ImBridge.Core.Map;
using ImBridge.Core.Models;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;

namespace ImBridge.Windows;

/// <summary>
/// Discovers a paired phone's Message Access Service over RFCOMM and drives a
/// <see cref="MapClient"/> against it. This is the Windows entry point the app
/// talks to.
/// </summary>
public sealed class MapBluetoothClient : IAsyncDisposable
{
    // MAP Message Access Server service class UUID (0x1132).
    private static readonly Guid MasServiceUuid = Guid.Parse("00001132-0000-1000-8000-00805F9B34FB");

    private RfcommObexTransport? _transport;
    private MapClient? _map;

    public bool IsConnected => _map is not null;

    /// <summary>List paired devices that advertise a Message Access Service.</summary>
    public static async Task<IReadOnlyList<DeviceInformation>> FindPhonesAsync()
    {
        string selector = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.FromUuid(MasServiceUuid));
        var devices = await DeviceInformation.FindAllAsync(selector);
        return devices.ToList();
    }

    public async Task ConnectAsync(DeviceInformation device, CancellationToken ct = default)
    {
        var service = await RfcommDeviceService.FromIdAsync(device.Id);
        if (service is null)
            throw new InvalidOperationException(
                "Could not open the Message Access Service. On the iPhone, open Settings > Bluetooth, " +
                "tap this PC, and enable \"Show Notifications\" / sync so MAP is exposed.");

        _transport = new RfcommObexTransport(service);
        _map = new MapClient(_transport);
        await _map.ConnectAsync(ct);
    }

    /// <summary>Pull a folder's listing and resolve each entry into a full message.</summary>
    public async Task<MessageStore> LoadFolderAsync(string folder = "telecom/msg/inbox",
        ushort maxCount = 100, CancellationToken ct = default)
    {
        if (_map is null) throw new InvalidOperationException("Not connected.");

        await _map.SetFolderAsync(folder, ct);
        var entries = await _map.GetMessageListingAsync(maxCount, ct);

        var store = new MessageStore();
        foreach (var entry in entries)
        {
            var message = MessageStore.ToMessage(entry);

            // The listing carries only a subject preview; fetch the body for recent ones.
            try
            {
                var bmsg = await _map.GetMessageAsync(entry.Handle, ct);
                if (!string.IsNullOrWhiteSpace(bmsg.Body))
                    message = message with { Body = bmsg.Body };
            }
            catch (MapException) { /* keep the listing preview if the body fetch fails */ }

            store.Upsert(message);
        }
        return store;
    }

    public Task SendAsync(string recipient, string text, CancellationToken ct = default)
    {
        if (_map is null) throw new InvalidOperationException("Not connected.");
        return _map.SendMessageAsync(recipient, text, ct: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_map is not null) await _map.DisconnectAsync();
        if (_transport is not null) await _transport.DisposeAsync();
    }
}
