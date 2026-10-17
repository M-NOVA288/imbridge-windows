using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace ImBridge.Windows.Ancs;

/// <summary>
/// Subscribes to the iPhone's ANCS Notification Source over BLE and raises an
/// event whenever a notification is added. The app listens for Social-category
/// adds (where iMessage/SMS land) to know when to refresh over MAP.
/// </summary>
public sealed class AncsClient : IAsyncDisposable
{
    private BluetoothLEDevice? _device;
    private GattCharacteristic? _notificationSource;

    /// <summary>Raised on the BLE callback thread for each incoming notification.</summary>
    public event EventHandler<AncsNotification>? NotificationReceived;

    /// <summary>Connect by the Bluetooth address of an already-paired iPhone.</summary>
    public async Task ConnectAsync(ulong bluetoothAddress)
    {
        _device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
        if (_device is null)
            throw new InvalidOperationException("Could not open the iPhone as a BLE device.");

        var services = await _device.GetGattServicesForUuidAsync(AncsConstants.ServiceUuid);
        if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
            throw new InvalidOperationException(
                "ANCS not found. Pair the iPhone with this PC and allow notification sharing.");

        var service = services.Services[0];
        var chars = await service.GetCharacteristicsForUuidAsync(AncsConstants.NotificationSourceUuid);
        if (chars.Status != GattCommunicationStatus.Success || chars.Characteristics.Count == 0)
            throw new InvalidOperationException("ANCS Notification Source characteristic not available.");

        _notificationSource = chars.Characteristics[0];
        _notificationSource.ValueChanged += OnValueChanged;

        var status = await _notificationSource.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify);
        if (status != GattCommunicationStatus.Success)
            throw new InvalidOperationException($"Could not subscribe to ANCS notifications: {status}.");
    }

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var reader = DataReader.FromBuffer(args.CharacteristicValue);
        if (reader.UnconsumedBufferLength < 8) return;
        var bytes = new byte[8];
        reader.ReadBytes(bytes);
        NotificationReceived?.Invoke(this, AncsNotification.Parse(bytes));
    }

    public ValueTask DisposeAsync()
    {
        if (_notificationSource is not null)
            _notificationSource.ValueChanged -= OnValueChanged;
        _device?.Dispose();
        return ValueTask.CompletedTask;
    }
}
