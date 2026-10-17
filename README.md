# ImBridge

**Read and send iMessage/SMS from a Windows PC, through your own paired iPhone, over Bluetooth.**

ImBridge is a Windows desktop app that talks to your iPhone using two public Bluetooth profiles the phone already supports:

- **MAP (Message Access Profile)** to read your message folders and send new messages.
- **ANCS (Apple Notification Center Service)** to get an instant signal when a new message arrives, so the app can refresh.

Your iPhone performs all the Apple-side authentication. ImBridge never impersonates Apple hardware and never touches Apple's servers directly, so there is no protocol to reverse-engineer and nothing for Apple to revoke. This is the same mechanism Microsoft Phone Link and Intel Unison use.

> **Status:** working protocol engine with a full test suite, real Windows Bluetooth transport, and a WinUI 3 UI shell. The first end-to-end run against a physical iPhone happens on your PC — see *Try it with your phone*.

---

## Why this is the honest way to do it

There are two ways to put iMessage on Windows:

| | ImBridge (this project) | The "Beeper Mini" route |
|---|---|---|
| How | Bluetooth MAP/ANCS through your own iPhone | Impersonate Apple hardware, talk to Apple's push servers directly |
| Needs your iPhone nearby | Yes | No |
| Reverse-engineers Apple crypto | No | Yes |
| Survives Apple updates | Yes (public Bluetooth standards) | No — Apple has repeatedly shut this down |

ImBridge takes the first route on purpose. It's legitimate, stable, and something you can put your name on.

## What it can and can't do

**Can:** list recent conversations, read message bodies, send a message, and get notified the instant one arrives, as long as the iPhone is paired and in Bluetooth range.

**Can't (by design of the Bluetooth profiles, not a bug):**
- Group chats and media (photos, tapbacks) aren't exposed over MAP.
- No full history — you see what the phone shares from the requested folders.
- Whether an outgoing message leaves as a blue iMessage or a green SMS is the **iPhone's** decision, not ImBridge's.

These are the same limits every Bluetooth-based bridge has, including Phone Link. If you need group chats and full history with no Mac, the only alternative is the device-spoofing route, which this project deliberately does not take.

## How it works

```
┌────────────────────────── Windows PC ──────────────────────────┐
│                                                                 │
│  ImBridge.App (WinUI 3)                                         │
│      │  conversations, compose, status                          │
│  ImBridge.Windows                                               │
│      │  RfcommObexTransport  ── RFCOMM socket ──┐               │
│      │  AncsClient           ── BLE GATT ──┐    │               │
│  ImBridge.Core  (no Windows dependency)    │    │               │
│      OBEX packets · MAP client · bMessage  │    │               │
└────────────────────────────────────────────┼────┼──────────────┘
                                              │    │   Bluetooth
                                   ┌──────────┴────┴──────────┐
                                   │          iPhone          │
                                   │  MAP server · ANCS · iMessage/SMS
                                   └──────────────────────────┘
```

The design keeps every byte of protocol logic in **`ImBridge.Core`**, which has no Windows dependency and is fully unit-tested. Bluetooth lives in **`ImBridge.Windows`** behind a one-method `IObexTransport` interface, so the MAP client can be driven by a fake transport in tests.

### The flow when you send a message

1. You type in the WinUI app and click **Send**.
2. `MapClient` builds a **bMessage** envelope (`BMessage.BuildForSend`) addressed to the recipient.
3. It `SETPATH`s to `telecom/msg/outbox` and `PUT`s the bMessage over OBEX, splitting it across packets if needed.
4. The iPhone receives it over MAP and sends it as iMessage or SMS.

## Repository layout

```
src/ImBridge.Core/        net8.0 — protocol engine, no OS dependency
  Obex/                   OBEX packet + header encode/decode, transport interface
  Map/                    MAP client, message-listing XML, bMessage, app parameters
  Models/                 Message, Conversation
  MessageStore.cs         groups messages into conversations
src/ImBridge.Windows/     net8.0-windows — real Bluetooth
  RfcommObexTransport.cs  OBEX over an RFCOMM socket
  MapBluetoothClient.cs   device discovery + high-level load/send
  Ancs/                   ANCS client over BLE GATT
src/ImBridge.App/         WinUI 3 desktop UI
tests/ImBridge.Core.Tests/ xUnit tests over recorded byte fixtures
```

## Build and test

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download). The protocol engine and its tests run on any OS:

```bash
dotnet test tests/ImBridge.Core.Tests/ImBridge.Core.Tests.csproj
```

The Bluetooth library and the app are Windows-only. On Windows (with the "​.NET desktop development" and "Windows App SDK" components, or just the SDK for a command-line build):

```bash
dotnet build src/ImBridge.Windows/ImBridge.Windows.csproj -c Release
dotnet build src/ImBridge.App/ImBridge.App.csproj -c Release -p:Platform=x64
```

CI (GitHub Actions) runs the Core tests on Linux and builds the Windows projects on `windows-latest` on every push.

## Try it with your phone

1. On the iPhone: **Settings → Bluetooth**, pair with your PC. When Windows asks, allow notifications/sync so the phone exposes MAP and ANCS.
2. Build and run `ImBridge.App` on the PC (an unpackaged WinUI app, so it's a plain `.exe`).
3. Click **Connect to iPhone**. The app finds the paired phone's Message Access Service, pulls the inbox, and lists conversations.
4. Pick a conversation and send.

If **Connect** finds no device, the phone isn't exposing MAP yet — re-check the Bluetooth pairing and that notification sharing is enabled.

## Roadmap

- [ ] Wire `AncsClient` into the UI so the inbox refreshes the instant a message arrives
- [ ] Register an MNS (Message Notification Service) server so the phone pushes new-message events over MAP too
- [ ] Conversation threading across the inbox, sent, and outbox folders
- [ ] A background service so messages arrive without the app open
- [ ] Packaged (MSIX) build for easy install

## What this is and isn't

This is a focused, legitimate messaging bridge built on documented Bluetooth standards. It is **not** a way to use iMessage without an iPhone, and it does not reverse-engineer or circumvent any Apple security system. If Apple's Bluetooth behavior changes, the affected capability simply isn't available — nothing here depends on an exploit.

## License

MIT — see [LICENSE](LICENSE).
