# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.2] - 2026-06-19

### Changed
- Added `NowNonce()`, `TimestampFromNowNonce(byte[])`, and `EntropyFromNowNonce(byte[])` to the `ITime` interface using default interface methods. This keeps the interface in sync with the `Clock` class without breaking existing implementations of `ITime`.

## [3.0.0] - 2026-06-19

### Added
- `NowNonce()` method — generates a 12-byte nonce (8-byte timestamp + 4-byte entropy) useful for cryptographic operations.
- `TimestampFromNowNonce(byte[])` and `EntropyFromNowNonce(byte[])` helper methods.
- Thread safety improvements across the `Clock` singleton (including protection against concurrent NTP sync calls).

### Changed
- Consolidated the entire library into a single feature file (`UtcMilliTime.cs`).
- Prepared the project for publishing both normal and source-only NuGet packages.
- Minor internal refactoring for maintainability.

## [2.2.3] - 2026-02-04

### Fixed
- NuGet not displaying the latest README.md on the package page.

## [2.2.2] - 2026-02-03

### Fixed
- NuGet not displaying the latest README.md (repeat of 2.2.1 fix).

## [2.2.1] - 2026-02-03

### Fixed
- Nullability warnings.
- Improved NuGet README display.

## [2.2.0] - 2026-02-03

### Added
- Chaining extension methods for Unix timestamps (`AddDays`, `AddHours`, `AddMinutes`, `AddSeconds`, `SubtractDays`, etc.).

## [2.1.0] - 2025-11-05

### Changed
- Updated for .NET 10 compatibility (still supports .NET 8+).
- Improved timing precision to 1 millisecond.

## [2.0.0] - 2025-07-11

### Changed
- Major cross-platform update. Now supports .NET 8 and later.
- Dropped legacy .NET Framework support.

## [1.0.1] - 2019-10-28

### Added
- Initial release targeting .NET Standard 2.0 (Windows-focused, compatible with .NET Framework 4.6.1+ and .NET Core 2.0+).
