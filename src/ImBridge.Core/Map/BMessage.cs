using System.Text;

namespace ImBridge.Core.Map;

/// <summary>
/// The bMessage object exchanged by MAP: a small text envelope that wraps the
/// message body plus sender/recipient vCards. We parse the pieces we need from
/// a received bMessage, and build a minimal one for sending.
/// </summary>
public sealed class BMessage
{
    public string Status { get; set; } = "UNREAD";          // READ | UNREAD
    public string Type { get; set; } = "SMS_GSM";           // SMS_GSM | SMS_CDMA | MMS | EMAIL | IM
    public string Folder { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string SenderAddress { get; set; } = "";
    public string RecipientAddress { get; set; } = "";
    public string Body { get; set; } = "";

    // ---- parsing ------------------------------------------------------------

    public static BMessage Parse(string text)
    {
        var bm = new BMessage();
        var lines = text.Replace("\r\n", "\n").Split('\n');

        bool inEnvelope = false, inBody = false, inMsg = false, firstVCardIsSender = true;
        var body = new StringBuilder();
        string? pendingVCardName = null, pendingVCardTel = null, pendingVCardEmail = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            if (inMsg)
            {
                if (line == "END:MSG") { inMsg = false; continue; }
                if (body.Length > 0) body.Append('\n');
                body.Append(line);
                continue;
            }

            if (line.StartsWith("STATUS:")) bm.Status = line[7..].Trim();
            else if (line.StartsWith("TYPE:")) bm.Type = line[5..].Trim();
            else if (line.StartsWith("FOLDER:")) bm.Folder = line[7..].Trim();
            else if (line == "BEGIN:BENV") inEnvelope = true;
            else if (line == "BEGIN:BBODY") inBody = true;
            else if (line == "BEGIN:MSG") inMsg = true;
            else if (line == "BEGIN:VCARD")
            {
                pendingVCardName = pendingVCardTel = pendingVCardEmail = null;
            }
            else if (line.StartsWith("FN:")) pendingVCardName = line[3..].Trim();
            else if (line.StartsWith("N:") && pendingVCardName is null)
                pendingVCardName = line[2..].Trim().TrimEnd(';').Replace(";", " ").Trim();
            else if (line.StartsWith("TEL:")) pendingVCardTel = line[4..].Trim();
            else if (line.StartsWith("TEL;")) pendingVCardTel = AfterColon(line);
            else if (line.StartsWith("EMAIL:")) pendingVCardEmail = line[6..].Trim();
            else if (line.StartsWith("EMAIL;")) pendingVCardEmail = AfterColon(line);
            else if (line == "END:VCARD")
            {
                // A bMessage lists the sender's vCard outside BENV; recipients inside.
                var addr = pendingVCardTel ?? pendingVCardEmail ?? "";
                if (!inEnvelope && firstVCardIsSender)
                {
                    bm.SenderName = pendingVCardName ?? "";
                    bm.SenderAddress = addr;
                    firstVCardIsSender = false;
                }
                else if (bm.RecipientAddress.Length == 0 && addr.Length > 0)
                {
                    bm.RecipientAddress = addr;
                }
            }
            else if (line == "END:BBODY") inBody = false;
            else if (line == "END:BENV") inEnvelope = false;
        }

        bm.Body = body.ToString();
        return bm;
    }

    private static string AfterColon(string line)
    {
        int i = line.IndexOf(':');
        return i >= 0 ? line[(i + 1)..].Trim() : "";
    }

    // ---- building (for sending) ---------------------------------------------

    /// <summary>
    /// A minimal, spec-shaped bMessage for a PUT to the outbox. The charset line
    /// and CRLF endings match what MSE implementations expect.
    /// </summary>
    public string BuildForSend()
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:BMSG\r\n");
        sb.Append("VERSION:1.0\r\n");
        sb.Append("STATUS:UNREAD\r\n");
        sb.Append($"TYPE:{Type}\r\n");
        sb.Append("FOLDER:telecom/msg/outbox\r\n");
        sb.Append("BEGIN:VCARD\r\nVERSION:2.1\r\nN:;;;;\r\nEND:VCARD\r\n");
        sb.Append("BEGIN:BENV\r\n");
        sb.Append("BEGIN:VCARD\r\nVERSION:2.1\r\n");
        sb.Append($"N:;;;;\r\nTEL:{RecipientAddress}\r\n");
        sb.Append("END:VCARD\r\n");
        sb.Append("BEGIN:BBODY\r\n");
        var bodyBytes = Encoding.UTF8.GetByteCount(Body);
        sb.Append("CHARSET:UTF-8\r\n");
        sb.Append($"LENGTH:{bodyBytes}\r\n");
        sb.Append("BEGIN:MSG\r\n");
        sb.Append(Body);
        sb.Append("\r\nEND:MSG\r\n");
        sb.Append("END:BBODY\r\n");
        sb.Append("END:BENV\r\n");
        sb.Append("END:BMSG\r\n");
        return sb.ToString();
    }
}
