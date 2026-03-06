### J2534-Sharp ###

J2534-Sharp handles all the details of operating with unmanaged SAE J2534 spec library and lets you deal with the important stuff.

Available on NuGet! [NuGet Gallery: J2534-Sharp]

# J2534-Sharp v2.0 - Modern .NET Rewrite

This is a complete rewrite of J2534-Sharp targeting .NET 10 with modern C# features, zero-allocation hot paths, and a Result pattern for error handling.

## Key Improvements

### 🚀 Performance
- **Zero heap allocations** in message send/receive hot path
- **Stack-allocated buffers** using `Span<T>` and `stackalloc`
- **Pinned managed arrays** for the message buffer (one allocation per channel, reused)
- **Single-copy data path**: native buffer → `Message.Data` array

### 🎯 Modern .NET Features
- **.NET 10** with C# 14
- **`Span<T>` and `ReadOnlySpan<T>`** for zero-copy data handling
- **`unsafe` code** for direct native memory access
- **`NativeLibrary`** API instead of kernel32 P/Invoke
- **Records** for data carriers (`APIInfo`, `CarDAQInfo`)
- **Nullable reference types** enabled

### 📦 Simplified Architecture
- **Eliminated 8 `Heap*` classes** - replaced with stack locals and `NativeMessageBuffer`
- **Eliminated 3 `Common/` base classes** - simple `IDisposable` pattern
- **`Message` as readonly struct** - immutable, stack-friendly
- **`J2534Result<T>`** - Railway-oriented programming pattern for error handling

### 🔧 Breaking Changes (By Design)
- **Namespace remains** `SAE.J2534` - same as before
- **Class names changed**: `API` → `J2534API`, `Device` → `J2534Device`, `Channel` → `J2534Channel`
- **Result pattern everywhere** - no more exceptions for expected outcomes
- **`ReadOnlySpan<byte>` parameters** instead of `IEnumerable<byte>`
- **Modern async-ready** (though J2534 itself is synchronous)

## Quick Start

### Loading an API

```csharp
using SAE.J2534;

// Discover registered APIs
foreach (var info in J2534APIFactory.DiscoverAPIs())
{
    Console.WriteLine($"{info.Name}: {info.FileName}");
}

// Load a specific API
var apiResult = J2534APIFactory.LoadAPI(@"C:\path\to\j2534.dll");
if (!apiResult.IsSuccess)
{
    Console.WriteLine($"Failed to load API: {apiResult.ErrorMessage}");
    return;
}

using var api = apiResult.Value;
Console.WriteLine($"Loaded {api.Signature}");
```

### Opening a Device and Channel

```csharp
// Open device
var deviceResult = api.OpenDevice();
if (!deviceResult.IsSuccess)
{
    Console.WriteLine($"Failed to open device: {deviceResult.ErrorMessage}");
    return;
}

using var device = deviceResult.Value;
Console.WriteLine($"Device: {device.DeviceName}");
Console.WriteLine($"Firmware: {device.FirmwareVersion}");

// Open channel
var channelResult = device.OpenChannel(
    Protocol.ISO15765,
    Baud.ISO15765_500000,
    ConnectFlag.NONE);

if (!channelResult.IsSuccess)
{
    Console.WriteLine($"Failed to open channel: {channelResult.ErrorMessage}");
    return;
}

using var channel = channelResult.Value;
```

### Sending and Receiving Messages

```csharp
// Send a message (zero allocation with stackalloc)
Span<byte> txData = stackalloc byte[] { 0x01, 0x00 };
var sendResult = channel.SendMessage(txData);

if (!sendResult.IsSuccess)
{
    Console.WriteLine($"Send failed: {sendResult.ErrorMessage}");
    return;
}

// Receive messages
var rxResult = channel.ReadMessages(10, timeoutMs: 1000);

if (rxResult.IsSuccess)
{
    foreach (var msg in rxResult.Messages)
    {
        Console.WriteLine($"RX: {BitConverter.ToString(msg.Data)}");
    }
}
else if (rxResult.IsTimeout)
{
    Console.WriteLine("No messages received (timeout)");
}
```

### Using the Result Pattern

```csharp
// Pattern matching
var result = channel.GetConfig(Parameter.DATA_RATE);
result.Match(
    onSuccess: value => Console.WriteLine($"Data rate: {value}"),
    onError: (code, msg) => Console.WriteLine($"Error {code}: {msg}")
);

// Chaining operations
var voltage = device.MeasureBatteryVoltage()
    .Map(v => v / 1000.0) // Convert mV to V
    .GetValueOrDefault(0.0);

Console.WriteLine($"Battery: {voltage:F2}V");

// Railway-oriented programming
var configResult = channel.GetConfig(Parameter.LOOPBACK)
    .Bind(value => channel.SetConfig(Parameter.LOOPBACK, value == 0 ? 1 : 0))
    .Bind(_ => channel.GetConfig(Parameter.LOOPBACK));

if (configResult.IsSuccess)
{
    Console.WriteLine($"Loopback toggled to: {configResult.Value}");
}
```

### Message Filters

```csharp
// Create a pass-all filter
var filter = new MessageFilter(UserFilterType.PASSALL, Array.Empty<byte>());
var filterResult = channel.StartMessageFilter(filter);

if (filterResult.IsSuccess)
{
    Console.WriteLine($"Filter started with ID: {filterResult.Value}");
}

// ISO15765 flow control filter
var iso15765Filter = new MessageFilter(
    UserFilterType.STANDARDISO15765,
    new byte[] { 0x00, 0x00, 0x07, 0xE8 } // Source address
);

var filterResult2 = channel.StartMessageFilter(iso15765Filter);
```

### Periodic Messages

```csharp
// Start a periodic message
var periodicMsg = new PeriodicMessage(
    data: new byte[] { 0x3E, 0x00 }, // Tester present
    interval: 2000 // Every 2 seconds
);

var periodicResult = channel.StartPeriodicMessage(periodicMsg);

if (periodicResult.IsSuccess)
{
    Console.WriteLine($"Periodic message started with ID: {periodicResult.Value}");
    
    // Let it run...
    Thread.Sleep(10000);
    
    // Stop it
    channel.StopPeriodicMessage(periodicResult.Value);
}
```

## Architecture Overview

```
J2534APIFactory (static)
    ↓ LoadAPI()
J2534API (IDisposable)
    ↓ OpenDevice()
J2534Device (IDisposable)
    ↓ OpenChannel()
J2534Channel (IDisposable)
    → NativeMessageBuffer (pinned array, reused)
```

### Memory Management

- **API**: Holds `NativeLibrary` handle, disposed when API is disposed
- **Device**: Tracks child channels, disposes them on device disposal
- **Channel**: Owns a `NativeMessageBuffer`, freed on channel disposal
- **NativeMessageBuffer**: Pinned `byte[]` via `GCHandle`, unpinned on dispose

### Thread Safety

- All API calls are locked via a shared `_syncRoot` object (per API instance)
- Each channel can optionally have its own lock (future enhancement)
- `J2534APIFactory` cache is thread-safe

## Migration Guide from v1.x

| v1.x | v2.0 |
|------|------|
| `APIFactory.GetAPI()` | `J2534APIFactory.LoadAPI()` |
| `API.GetDevice()` | `J2534API.OpenDevice()` |
| `Device.GetChannel()` | `J2534Device.OpenChannel()` |
| `Channel.GetMessages()` | `Channel.ReadMessages()` |
| `Channel.SendMessage(IEnumerable<byte>)` | `Channel.SendMessage(ReadOnlySpan<byte>)` |
| `try { api.Method(); } catch (J2534Exception)` | `var result = api.Method(); if (!result.IsSuccess)` |
| `HeapMessageArray` | `NativeMessageBuffer` (internal) |
| `GetMessageResults` | `GetMessagesResult` (record struct) |

## Performance Comparison

### v1.x (OLD)
```csharp
using (var hMsg = new HeapMessageArray(protocol, 10))  // ← Heap alloc
{
    hMsg.FromDataBytes(data, txFlags);  // ← Marshal + copies
    api.PTWriteMsgs(...);
}  // ← Dispose + finalizer
```

**Allocations**: `HeapMessageArray` object + unmanaged memory + GC pressure from finalizer

### v2.0 (NEW)
```csharp
_messageBuffer.WriteSingleMessage(data, txFlags);  // ← Write to pinned array
api.PTWriteMsgs(...);
// No dispose, buffer is reused
```

**Allocations**: Zero (buffer pre-allocated per channel)

## What's Next

- Port all definition enums (already good, just copy)
- Add XML documentation
- Unit tests
- NuGet package
- Benchmarks vs. v1.x

## License

MIT - see LICENSE file
