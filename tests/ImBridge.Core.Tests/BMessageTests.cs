using ImBridge.Core.Map;
using Xunit;

namespace ImBridge.Core.Tests;

public class BMessageTests
{
    private const string Sample =
        "BEGIN:BMSG\r\nVERSION:1.0\r\nSTATUS:READ\r\nTYPE:SMS_GSM\r\nFOLDER:telecom/msg/inbox\r\n" +
        "BEGIN:VCARD\r\nVERSION:2.1\r\nN:;Alex;;;\r\nTEL:+15551234567\r\nEND:VCARD\r\n" +
        "BEGIN:BENV\r\nBEGIN:VCARD\r\nVERSION:2.1\r\nN:;;;;\r\nTEL:+15559999999\r\nEND:VCARD\r\n" +
        "BEGIN:BBODY\r\nCHARSET:UTF-8\r\nLENGTH:16\r\nBEGIN:MSG\r\nHey, you around?\r\nEND:MSG\r\n" +
        "END:BBODY\r\nEND:BENV\r\nEND:BMSG\r\n";

    [Fact]
    public void Parses_sender_recipient_and_body()
    {
        var bm = BMessage.Parse(Sample);
        Assert.Equal("READ", bm.Status);
        Assert.Equal("SMS_GSM", bm.Type);
        Assert.Equal("Alex", bm.SenderName);
        Assert.Equal("+15551234567", bm.SenderAddress);
        Assert.Equal("+15559999999", bm.RecipientAddress);
        Assert.Equal("Hey, you around?", bm.Body);
    }

    [Fact]
    public void Build_for_send_embeds_recipient_and_body()
    {
        var bm = new BMessage { RecipientAddress = "+15551234567", Body = "On my way" };
        var text = bm.BuildForSend();

        Assert.Contains("BEGIN:BMSG", text);
        Assert.Contains("TEL:+15551234567", text);
        Assert.Contains("BEGIN:MSG\r\nOn my way\r\nEND:MSG", text);
        Assert.Contains("CHARSET:UTF-8", text);
    }

    [Fact]
    public void Multiline_body_is_preserved()
    {
        var sample = Sample.Replace("Hey, you around?", "line one\r\nline two");
        var bm = BMessage.Parse(sample);
        Assert.Equal("line one\nline two", bm.Body);
    }
}
