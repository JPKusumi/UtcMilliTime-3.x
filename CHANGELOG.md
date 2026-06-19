# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] - 2026-xx-xx

### Added
- `NowNonce()` method — generates a 12-byte nonce (8-byte timestamp + 4-byte entropy) useful for cryptographic operations.
- `TimestampFromNowNonce(byte[])` and `EntropyFromNowNonce(byte[])` helper methods.
- Thread safety improvements across the `Clock` singleton (including protection against concurrent NTP sync calls).

### Changed
- Consolidated the entire library into a single feature file (`UtcMilliTime.cs`).
- Prepared the project for publishing both normal and source-only NuGet packages.
- Minor internal refactoring for maintainability.

## [2.2.3] - 2025-xx-xx

### Fixed
- NuGet not displaying the latest README.md on the package page.

## [2.2.2] - 2025-xx-xx

### Fixed
- NuGet not displaying the latest README.md (repeat of 2.2.1 fix).

## [2.2.1] - 2025-xx-xx

### Fixed
- Nullability warnings.
- Improved NuGet README display.

## [2.2.0] - 2025-xx-xx

### Added
- Chaining extension methods for Unix timestamps (`AddDays`, `AddHours`, `AddMinutes`, `AddSeconds`, `SubtractDays`, etc.).

## [2.1.0] - 2024-xx-xx

### Changed
- Updated for .NET 10 compatibility (still supports .NET 8+).
- Improved timing precision to 1 millisecond.

## [2.0.0] - 2024-xx-xx

### Changed
- Major cross-platform update. Now supports .NET 8 and later.
- Dropped legacy .NET Framework support.

## [1.0.1] - 2019-xx-xx

### Added
- Initial release targeting .NET Standard 2.0 (Windows-focused, compatible with .NET Framework 4.6.1+ and .NET Core 2.0+).