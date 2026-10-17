using System.Collections.ObjectModel;
using System.ComponentModel;
using ImBridge.Core;
using ImBridge.Core.Models;
using ImBridge.Windows;

namespace ImBridge.App.ViewModels;

/// <summary>
/// Drives the UI: find the paired iPhone, pull the inbox over MAP, expose
/// conversations, and send. Deliberately thin — the protocol work lives in
/// ImBridge.Core and the Bluetooth work in ImBridge.Windows.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly MapBluetoothClient _client = new();

    public ObservableCollection<Conversation> Conversations { get; } = new();

    private string _status = "Not connected.";
    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public bool IsConnected => _client.IsConnected;

    public async Task ConnectAsync()
    {
        try
        {
            Status = "Looking for a paired iPhone…";
            var phones = await MapBluetoothClient.FindPhonesAsync();
            if (phones.Count == 0)
            {
                Status = "No paired phone exposes Message Access. Pair your iPhone and enable sync.";
                return;
            }

            Status = $"Connecting to {phones[0].Name}…";
            await _client.ConnectAsync(phones[0]);

            Status = "Loading inbox…";
            var store = await _client.LoadFolderAsync("telecom/msg/inbox");

            Conversations.Clear();
            foreach (var convo in store.Ordered())
                Conversations.Add(convo);

            Status = $"Connected. {Conversations.Count} conversation(s).";
            OnPropertyChanged(nameof(IsConnected));
        }
        catch (Exception ex)
        {
            Status = $"Connection failed: {ex.Message}";
        }
    }

    public async Task SendAsync(Conversation conversation, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            Status = "Sending…";
            await _client.SendAsync(conversation.Address, text);

            conversation.Messages.Add(new Message
            {
                Handle = Guid.NewGuid().ToString("N"),
                Sender = "You",
                SenderAddress = conversation.Address,
                Body = text,
                Timestamp = DateTimeOffset.Now,
                Direction = MessageDirection.Outgoing,
                Read = true,
            });
            Status = "Sent.";
        }
        catch (Exception ex)
        {
            Status = $"Send failed: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
