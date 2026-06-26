
| **[JPKusumi.com](https://jpkusumi.com) presents—** |
|:---------------------:|

# UtcMilliTime

UtcMilliTime is a lightweight C# library that provides reliable Unix millisecond timestamps (`long`), similar to JavaScript's `Date.now()`. It includes built-in NTP synchronization, high-resolution monotonic time, and strong support for unit testing.

**Version 3.1** introduces `TestClock` — a fully controllable clock designed for deterministic unit testing of time-dependent code.

On NuGet at: https://www.nuget.org/packages/UtcMilliTime/  
On GitHub at: https://github.com/JPKusumi/UtcMilliTime-3.x

## What's New in v3.1

- `TestClock` — a controllable implementation of `ITime` for unit testing
- `AdvanceDays()`, `AdvanceWeeks()`, `AdvanceMonths()`, and `AdvanceYears()` methods
- `RaiseNetworkTimeAcquired()` methods to simulate NTP events in tests
- Improved testability for timeouts, expirations, retries, and scheduled logic

See the [CHANGELOG](CHANGELOG.md) for the full version history.

## Overview

UtcMilliTime provides `long` timestamps (milliseconds since 1970-01-01 UTC). It starts with device time and can optionally synchronize with NTP servers. The main entry point is the `Clock` singleton, which implements the `ITime` interface.

## Installation

```bash
dotnet add package UtcMilliTime --version 3.1.0
```

## Basic Usage

```csharp
using UtcMilliTime;

var clock = Clock.Time;
clock.SuppressNetworkCalls = false;   // Enable NTP synchronization

long timestamp = clock.Now;
string iso = timestamp.ToIso8601String();

// Cryptographic helper (v3.0+)
byte[] nonce = clock.NowNonce();
long ts = clock.TimestampFromNowNonce(nonce);
uint entropy = clock.EntropyFromNowNonce(nonce);
```

## API Reference

### Core API (Clock / ITime)

| Member                        | Description                                                                 | Notes |
|------------------------------|-----------------------------------------------------------------------------|-------|
| `Now`                        | Current Unix timestamp in milliseconds                                      | — |
| `NowNonce()`                 | Returns a 12-byte nonce (8-byte timestamp + 4-byte entropy)                 | Useful for cryptography |
| `SuppressNetworkCalls`       | When `true`, disables NTP synchronization                                   | Defaults to `true` |
| `SelfUpdateAsync()`          | Manually triggers an NTP synchronization                                    | — |
| `Initialized`                | Indicates whether the clock has been initialized                            | `true` after construction |
| `Synchronized`               | Indicates whether the clock has successfully synchronized with an NTP server | — |
| `Skew`                       | Difference between device time and NTP time (in ms)                         | Updated after sync |
| `NetworkTimeAcquired`        | Event raised after a successful NTP synchronization                         | — |
| `DefaultServer`              | NTP server used for synchronization                                         | Falls back to `pool.ntp.org` if not set |

### TestClock API (v3.1+)

`TestClock` is a controllable implementation of `ITime` designed for unit testing. Time only advances when explicitly instructed.

| Member                              | Description                                      | Notes |
|-------------------------------------|--------------------------------------------------|-------|
| `new TestClock(long initialTime)`   | Creates a test clock starting at the given time  | Time stays frozen until advanced |
| `TestClock.FromCurrentTime()`       | Creates a test clock starting at the current real time | — |
| `SetTime(long timestamp)`           | Sets the clock to an absolute timestamp          | — |
| `Advance(TimeSpan)`                 | Advances time by a `TimeSpan`                    | — |
| `AdvanceMilliseconds(long)`         | Advances time by milliseconds                    | — |
| `AdvanceSeconds(int)`               | Advances time by seconds                         | — |
| `AdvanceMinutes(int)`               | Advances time by minutes                         | — |
| `AdvanceHours(int)`                 | Advances time by hours                           | — |
| `AdvanceDays(int)`                  | Advances time by days                            | — |
| `AdvanceWeeks(int)`                 | Advances time by weeks                           | — |
| `AdvanceMonths(int)`                | Advances time by months (approximate)            | Uses 30 days per month |
| `AdvanceYears(int)`                 | Advances time by years (approximate)             | Uses 365 days per year |
| `RaiseNetworkTimeAcquired(...)`     | Manually raises the `NetworkTimeAcquired` event  | Useful for testing event handlers |

**Example:**

```csharp
var clock = new TestClock(1_700_000_000_000_000); // Start at a specific time
var clock = TestClock.FromCurrentTime();          // Start from real current time

clock.SetTime(1_800_000_000_000_000);

clock.Advance(TimeSpan.FromMinutes(30));
clock.AdvanceMilliseconds(5000);
clock.AdvanceSeconds(45);
clock.AdvanceMinutes(10);
clock.AdvanceHours(2);
clock.AdvanceDays(5);
clock.AdvanceWeeks(2);
clock.AdvanceMonths(1);
clock.AdvanceYears(1);

// Simulate an NTP sync in a test
clock.RaiseNetworkTimeAcquired("pool.ntp.org", latency: 45, skew: 12);
```

## Thread Safety (v3)

Version 3 includes thread safety improvements across the `Clock` singleton, particularly around concurrent access and NTP synchronization.

## Notes

- By default, `SuppressNetworkCalls = true` (uses only device time).
- Set `SuppressNetworkCalls = false` or call `SelfUpdateAsync()` to enable NTP synchronization.
- The `NetworkTimeAcquired` event fires after successful NTP syncs.

## Resources

- Website: [JPKusumi.com](https://jpkusumi.com)
- Discussions & Feedback: [GitHub Discussions](https://github.com/JPKusumi/GreenfieldPQC/discussions)

## License

MIT License
