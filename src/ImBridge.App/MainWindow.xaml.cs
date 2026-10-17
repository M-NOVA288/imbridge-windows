using ImBridge.App.ViewModels;
using ImBridge.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ImBridge.App;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        _vm.PropertyChanged += (_, _) => StatusText.Text = _vm.Status;
        ConversationList.ItemsSource = _vm.Conversations;
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        ConnectButton.IsEnabled = false;
        await _vm.ConnectAsync();
        ConnectButton.IsEnabled = true;
    }

    private void OnConversationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ConversationList.SelectedItem is Conversation convo)
            MessageList.ItemsSource = convo.Messages.OrderBy(m => m.Timestamp).ToList();
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        if (ConversationList.SelectedItem is not Conversation convo) return;
        var text = ComposeBox.Text;
        ComposeBox.Text = "";
        await _vm.SendAsync(convo, text);
        MessageList.ItemsSource = convo.Messages.OrderBy(m => m.Timestamp).ToList();
    }
}
