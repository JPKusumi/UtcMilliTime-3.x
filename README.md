
| **[JPKusumi.com](https://jpkusumi.com) presents—** |
|:---------------------:|

# UtcMilliTime

UtcMilliTime is a C# time component (software-defined clock) that yields Unix time milliseconds (`Int64`) timestamps, similar to JavaScript's `Date.now()`. It synchronizes with NTP servers and is cross-platform for .NET 8+.

**Version 3** is thread-safe and introduces `NowNonce()` — a 12-byte value (8-byte timestamp + 4-byte entropy) designed for cryptographic use cases such as nonces in encryption or JWT handling.

On NuGet at: https://www.nuget.org/packages/UtcMilliTime/  
On GitHub at: https://github.com/JPKusumi/UtcMilliTime-3.x

## What's New in v3.0

- `NowNonce()`, `TimestampFromNowNonce()`, and `EntropyFromNowNonce()` methods
- Thread safety improvements across the `Clock` singleton
- Consolidated into a single feature file for easier maintenance
- Prepared for both normal and source-only NuGet packaging

See the [CHANGELOG](CHANGELOG.md) for the full version history.

## Overview

UtcMilliTime provides `Int64` timestamps (milliseconds since 1/1/1970 UTC). It initializes with device time and can optionally synchronize with NTP servers. The clock uses a singleton pattern so that all parts of an application share the same time source.

## Installation

```bash
dotnet add package UtcMilliTime --version 3.0.2
```

## Basic Usage

```csharp
using UtcMilliTime;

var clock = Clock.Time;
clock.SuppressNetworkCalls = false;     // Allow NTP synchronization

long timestamp = clock.Now;
string iso = timestamp.ToIso8601String();

// New in v3
byte[] nonce = clock.NowNonce();
long ts = clock.TimestampFromNowNonce(nonce);
uint entropy = clock.EntropyFromNowNonce(nonce);
```

## Thread Safety (v3)

Version 3 includes thread safety improvements, particularly around concurrent access and network synchronization.

## Notes

- By default, `SuppressNetworkCalls = true` (uses only device time).
- Call `SelfUpdateAsync()` manually or set `SuppressNetworkCalls = false` to enable NTP sync.
- The `NetworkTimeAcquired` event can be used to react to successful synchronizations.

## Resources

For more information and updates, visit [JPKusumi.com](https://jpkusumi.com).

Discussion and feedback: [GitHub Discussions](https://github.com/JPKusumi/GreenfieldPQC/discussions)

## License

MIT License
