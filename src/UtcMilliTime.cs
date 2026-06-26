// =====================================================
// UtcMilliTime.cs
// Single Feature File - Version 3.x
// =====================================================
#region Using Directives
using System;
using System.Buffers.Binary;
using System.Diagnostics;  // For Stopwatch
using System.Net;          // For Dns and IPEndPoint
using System.Net.NetworkInformation;
using System.Net.Sockets;  // For Socket, AddressFamily, SocketType, and ProtocolType
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
#endregion
namespace UtcMilliTime
{
    #region Constants
    public static class Constants
    {
        public const short bytes_per_buffer = 48;
        public const short udp_port_number = 123;
        public const short second_milliseconds = 1000;
        public const short three_seconds = 3000;
        public const short dotnet_ticks_per_millisecond = 10000;
        public const int minute_milliseconds = 60000;
        public const int hour_milliseconds = 3600000;
        public const int day_milliseconds = 86400000;
        public const long ntp_to_unix_milliseconds = 2208988800000;
        public const long dotnet_to_unix_milliseconds = 62135596800000;
        public const string fallback_server = "pool.ntp.org";
        public const string iso_8601_without_milliseconds = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'";
        public const string iso_8601_with_milliseconds = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'";
    }
    #endregion
    #region Supporting Types
    /// <summary>
    /// Defines the public contract for time-related functionality.
    /// 
    /// New methods added in v3 (NowNonce and related extractors) were implemented 
    /// using default interface methods to avoid breaking changes for existing implementers.
    /// </summary>
    public interface ITime
    {
        string DefaultServer { get; set; }
        long DeviceBootTime { get; }
        long DeviceUpTime { get; }
        long DeviceUtcNow { get; }
        bool Initialized { get; }
        long Now { get; }
        long Skew { get; }
        bool SuppressNetworkCalls { get; set; }
        bool Synchronized { get; }

        event EventHandler<NTPEventArgs>? NetworkTimeAcquired;

        Task SelfUpdateAsync(string ntpServerHostName = Constants.fallback_server);

        byte[] NowNonce()
            => throw new NotSupportedException("NowNonce() is not supported by this ITime implementation.");

        long TimestampFromNowNonce(byte[] nonce)
            => throw new NotSupportedException("TimestampFromNowNonce() is not supported by this ITime implementation.");

        uint EntropyFromNowNonce(byte[] nonce)
            => throw new NotSupportedException("EntropyFromNowNonce() is not supported by this ITime implementation.");
    }
    public class NTPCallState
    {
        public bool priorSyncState;
        public byte[] buffer = new byte[Constants.bytes_per_buffer];
        public short methodsCompleted;
        public Socket? socket;
        public Stopwatch? latency;
        public Stopwatch? timer;
        public string serverResolved = string.Empty;

        public NTPCallState()
        {
            latency = Stopwatch.StartNew();
            buffer[0] = 0x1B;
        }

        public void OrderlyShutdown()
        {
            if (timer?.IsRunning == true)
            {
                timer.Stop();
            }
            timer = null;

            if (socket != null)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
                catch { }
                socket = null;
            }

            if (latency?.IsRunning == true)
            {
                latency.Stop();
            }
            latency = null;
        }
    }
    public class NTPEventArgs : EventArgs
    {
        public string Server { get; }
        public long Latency { get; }
        public long Skew { get; }

        public NTPEventArgs(string server, long latency, long skew)
        {
            Server = server;
            Latency = latency;
            Skew = skew;
        }
    }
    #endregion
    #region Clock Class
    public sealed class Clock : ITime
    {
        #region Private Fields
        private static readonly Lazy<Clock> instance = new(() => new Clock());
        private static bool successfully_synced;
        private static bool suppress_network_calls = true;
        private static long device_boot_time;
        private static NTPCallState? ntpCall;
        private static bool Indicated => !suppress_network_calls && !successfully_synced && NetworkInterface.GetIsNetworkAvailable();


        // High-res uptime fields
        private static long initQpcTimestamp;
        private static long qpcFrequency;
        private static long initialSystemUptimeMs;
        private static int _syncInProgress;
        #endregion
        #region Public Properties
        public static Clock Time => instance.Value;
        public bool Initialized => device_boot_time != 0;
        public bool SuppressNetworkCalls
        {
            get => suppress_network_calls;
            set
            {
                if (value != suppress_network_calls)
                {
                    suppress_network_calls = value;

                    if (Indicated && Interlocked.CompareExchange(ref _syncInProgress, 1, 0) == 0)
                    {
                        try
                        {
                            SelfUpdateAsync().SafeFireAndForget(false);
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _syncInProgress, 0);
                        }
                    }
                }
            }
        }
        public bool Synchronized => successfully_synced;
        public long DeviceBootTime => device_boot_time;
        public long DeviceUpTime => GetHighResUptime();
        public long DeviceUtcNow => GetDeviceTime();
        public long Now => Interlocked.Read(ref device_boot_time) + GetHighResUptime();
        public long Skew { get; private set; }
        public string DefaultServer { get; set; } = Constants.fallback_server;
        public event EventHandler<NTPEventArgs>? NetworkTimeAcquired;
        #endregion
        #region Construction & Initialization
        private Clock()
        {
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            Initialize();
            if (Indicated)
            {
                SelfUpdateAsync().SafeFireAndForget(false);
            }
        }
        public static Task<Clock> CreateAsync()
        {
            var clock = Time; // Ensure lazy singleton init (sync, device time)
            return Task.FromResult(clock); // Return immediately; no await needed yet
        }
        private void Initialize()
        {
            // Capture low-res system uptime first (ms since boot)
            initialSystemUptimeMs = Environment.TickCount64;

            // Estimate initial boot time using low-res uptime (as original)
            device_boot_time = GetDeviceTime() - initialSystemUptimeMs;
            successfully_synced = false;
            Skew = 0;

            // Capture high-res reference for deltas (simplified to Stopwatch)
            qpcFrequency = Stopwatch.Frequency;
            initQpcTimestamp = Stopwatch.GetTimestamp();
        }
        #endregion
        #region Private Methods
        private void NetworkChange_NetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            if (Indicated)
            {
                SelfUpdateAsync().SafeFireAndForget(false);
            }
        }
        private static long GetHighResUptime()
        {
            long currentQpc = Stopwatch.GetTimestamp();
            long qpcDelta = currentQpc - Interlocked.Read(ref initQpcTimestamp);
            long highResDeltaMs = (qpcDelta * 1000L) / Interlocked.Read(ref qpcFrequency);
            return Interlocked.Read(ref initialSystemUptimeMs) + highResDeltaMs;
        }
        private static long GetDeviceTime() => DateTime.UtcNow.Ticks / Constants.dotnet_ticks_per_millisecond - Constants.dotnet_to_unix_milliseconds;
        #endregion
        #region NowNonce Methods
        /// <summary>
        /// Generates a 12-byte NowNonce consisting of an 8-byte timestamp followed by 4 bytes of entropy.
        /// </summary>
        public byte[] NowNonce()
        {
            long timestamp = Now;
            byte[] nonce = new byte[12];

            BinaryPrimitives.WriteInt64BigEndian(nonce, timestamp);
            RandomNumberGenerator.Fill(nonce.AsSpan(8, 4));

            return nonce;
        }

        /// <summary>
        /// Extracts the timestamp from a previously generated NowNonce (12-byte format).
        /// </summary>
        public long TimestampFromNowNonce(byte[] nonce)
        {
            if (nonce == null || nonce.Length != 12)
                throw new ArgumentException("Nonce must be exactly 12 bytes.", nameof(nonce));

            return BinaryPrimitives.ReadInt64BigEndian(nonce);
        }

        /// <summary>
        /// Extracts the 4-byte entropy portion from a previously generated NowNonce.
        /// </summary>
        public uint EntropyFromNowNonce(byte[] nonce)
        {
            if (nonce == null || nonce.Length != 12)
                throw new ArgumentException("Nonce must be exactly 12 bytes.", nameof(nonce));

            return BinaryPrimitives.ReadUInt32BigEndian(nonce.AsSpan(8));
        }
        #endregion
        #region NTP / Network Sync Logic
        public async Task SelfUpdateAsync(string ntpServerHostName = Constants.fallback_server)
        {
            if (ntpCall != null)
            {
                return;
            }

            ntpCall = new NTPCallState
            {
                priorSyncState = successfully_synced
            };
            // latency already started in NTPCallState constructor - no need to start it here
            try
            {
                Initialize();
                if (!Initialized || !Indicated)
                {
                    return;
                }

                ntpServerHostName = ntpServerHostName == Constants.fallback_server && !string.IsNullOrEmpty(DefaultServer)
                    ? DefaultServer
                    : ntpServerHostName;

                ntpCall.serverResolved = ntpServerHostName;
                var addresses = await Dns.GetHostAddressesAsync(ntpServerHostName).ConfigureAwait(false);
                var ipEndPoint = new IPEndPoint(addresses[0], Constants.udp_port_number);

                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                {
                    ReceiveTimeout = Constants.three_seconds
                };

                ntpCall.socket = socket; // Assign for NTPCallState compatibility
                ntpCall.timer = Stopwatch.StartNew();
                await socket.ConnectAsync(ipEndPoint).ConfigureAwait(false);
                ntpCall.methodsCompleted += 1;

                await socket.SendAsync(ntpCall.buffer.AsMemory(0, Constants.bytes_per_buffer)).ConfigureAwait(false);
                ntpCall.methodsCompleted += 1;

                await socket.ReceiveAsync(ntpCall.buffer.AsMemory(0, Constants.bytes_per_buffer)).ConfigureAwait(false);
                ntpCall.methodsCompleted += 1;
                ntpCall.timer.Stop();

                long halfRoundTrip = ntpCall.timer.ElapsedMilliseconds / 2;
                const byte serverReplyTime = 40;
                ulong intPart = BitConverter.ToUInt32(ntpCall.buffer, serverReplyTime);
                ulong fractPart = BitConverter.ToUInt32(ntpCall.buffer, serverReplyTime + 4);
                intPart = SwapEndianness(intPart);
                fractPart = SwapEndianness(fractPart);
                var milliseconds = intPart * 1000 + fractPart * 1000 / 0x100000000L;
                long timeNow = (long)milliseconds - Constants.ntp_to_unix_milliseconds + halfRoundTrip;

                if (timeNow <= 0)
                {
                    successfully_synced = false;
                    return;
                }

                long highResUptime = GetHighResUptime();
                device_boot_time = timeNow - highResUptime;
                Skew = timeNow - GetDeviceTime(); // Simple original calc
                successfully_synced = ntpCall.methodsCompleted == 3;
                ntpCall.latency!.Stop();

                if (successfully_synced && !ntpCall.priorSyncState && NetworkTimeAcquired != null)
                {
                    NTPEventArgs args = new(ntpCall.serverResolved, ntpCall.latency?.ElapsedMilliseconds ?? 0, Skew);
                    NetworkTimeAcquired.Invoke(this, args);
                }
            }
            catch (Exception)
            {
                successfully_synced = false;
            }
            finally
            {
                ntpCall?.OrderlyShutdown();
                ntpCall = null;
            }
        }
        #endregion
        #region Internal Helpers
        private static uint SwapEndianness(ulong x) => (uint)(((x & 0x000000ff) << 24) +
            ((x & 0x0000ff00) << 8) +
            ((x & 0x00ff0000) >> 8) +
            ((x & 0xff000000) >> 24));
        #endregion
    }
    #endregion
    #region Extension Methods
#pragma warning disable RECS0165 // Async methods should return a Task (async void)
    /// <summary>
    /// Extension methods for working with UtcMilliTime (64-bit Unix timestamps in UTC milliseconds).
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// Converts a UtcMilliTime timestamp to an ISO-8601 string.
        /// </summary>
        /// <param name="timestamp">The UtcMilliTime value.</param>
        /// <param name="suppressMilliseconds">If true, omits milliseconds.</param>
        /// <returns>String like "2019-08-10T22:08:14.102Z".</returns>
        public static string ToIso8601String(this long timestamp, bool suppressMilliseconds = false)
        {
            long ticks = (timestamp + Constants.dotnet_to_unix_milliseconds) * Constants.dotnet_ticks_per_millisecond;
            var dateTime = new DateTime(ticks, DateTimeKind.Utc);
            return suppressMilliseconds
                ? dateTime.ToString(Constants.iso_8601_without_milliseconds)
                : dateTime.ToString(Constants.iso_8601_with_milliseconds);
        }

        /// <summary>
        /// Converts a DateTime to UtcMilliTime (truncates fractional ms).
        /// </summary>
        public static long ToUtcMilliTime(this DateTime given) => (given.ToUniversalTime().Ticks / Constants.dotnet_ticks_per_millisecond) - Constants.dotnet_to_unix_milliseconds;

        /// <summary>
        /// Converts a DateTimeOffset to UtcMilliTime (truncates fractional ms).
        /// </summary>
        public static long ToUtcMilliTime(this DateTimeOffset given) => given.ToUnixTimeMilliseconds();

        /// <summary>
        /// Converts a TimeSpan interval to UtcMilliTime (truncates fractional ms).
        /// </summary>
        public static long ToUtcMilliTime(this TimeSpan given) => (long)given.TotalMilliseconds;

        /// <summary>
        /// Converts UnixTimeSeconds to UtcMilliTime (multiplies by 1000).
        /// </summary>
        public static long ToUtcMilliTime(this long unixtimeSeconds) => unixtimeSeconds * 1000;

        /// <summary>
        /// Truncates UtcMilliTime to UnixTimeSeconds (divides by 1000).
        /// </summary>
        public static long ToUnixTime(this long timestamp) => timestamp / 1000;

        /// <summary>
        /// Extracts the millisecond part (0-999) from a UtcMilliTime timestamp.
        /// </summary>
        public static short MillisecondPart(this long timestamp) => (short)(timestamp % 1000);

        /// <summary>
        /// Converts UtcMilliTime to a UTC DateTime.
        /// </summary>
        public static DateTime ToUtcDateTime(this long timestamp) => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(timestamp);

        /// <summary>
        /// Converts UtcMilliTime to a local DateTime.
        /// </summary>
        public static DateTime ToLocalDateTime(this long timestamp) => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(timestamp).ToLocalTime();

        /// <summary>
        /// Converts UtcMilliTime to a DateTimeOffset (UTC, offset 0).
        /// </summary>
        public static DateTimeOffset ToDateTimeOffset(this long timestamp) => new(timestamp.ToUtcDateTime());

        /// <summary>
        /// Converts a UtcMilliTime interval to a TimeSpan (or from 1970 if absolute).
        /// </summary>
        public static TimeSpan ToTimeSpan(this long interval) => new(interval * Constants.dotnet_ticks_per_millisecond);
        /// <summary>
        /// The whole number of days in an interval found in UtcMilliTime
        /// </summary>
        /// <param name="interval">UtcMilliTime</param>
        /// <returns>int</returns>
        public static int IntervalDays(this long interval)
        {
            return TimeSpan.FromMilliseconds(interval).Days;
        }
        /// <summary>
        /// Whole hours in the remainder after days are removed from an interval
        /// </summary>
        /// <param name="interval">UtcMilliTime</param>
        /// <returns>int</returns>
        public static int IntervalHoursPart(this long interval)
        {
            return TimeSpan.FromMilliseconds(interval).Hours;
        }
        /// <summary>
        /// Whole minutes in the remainder after days and hours are removed from an interval
        /// </summary>
        /// <param name="interval">UtcMilliTime</param>
        /// <returns>int</returns>
        public static int IntervalMinutesPart(this long interval)
        {
            return TimeSpan.FromMilliseconds(interval).Minutes;
        }
        /// <summary>
        /// Whole seconds in the remainder after removing days, hours, and minutes from an interval
        /// </summary>
        /// <param name="interval">UtcMilliTime</param>
        /// <returns>int</returns>
        public static int IntervalSecondsPart(this long interval)
        {
            return TimeSpan.FromMilliseconds(interval).Seconds;
        }
        // Additive and subtractive operations for chaining in auth/timing flows (operate on Unix seconds for JWT claims, etc.)
        /// <summary>
        /// Truncates UtcMilliTime to UnixTimeSeconds (divides by 1000). Alias for ToUnixTime for explicitness.
        /// </summary>
        public static long ToUnixTimeSeconds(this long timestamp) => timestamp.ToUnixTime();

        /// <summary>
        /// Adds the specified number of days to a Unix time in seconds, returning a new timestamp.
        /// </summary>
        /// <param name="unixSeconds">The Unix time in seconds.</param>
        /// <param name="days">The number of days to add (must be non-negative).</param>
        /// <returns>A new Unix time in seconds.</returns>
        public static long AddDays(this long unixSeconds, int days)
        {
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days), "Days must be non-negative.");
            return unixSeconds + (days * 86400L);  // 86,400 seconds per day
        }

        /// <summary>
        /// Subtracts the specified number of days from a Unix time in seconds, returning a new timestamp.
        /// </summary>
        /// <param name="unixSeconds">The Unix time in seconds.</param>
        /// <param name="days">The number of days to subtract (must be non-negative).</param>
        /// <returns>A new Unix time in seconds.</returns>
        public static long SubtractDays(this long unixSeconds, int days)
        {
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days), "Days must be non-negative.");
            return unixSeconds - (days * 86400L);
        }

        /// <summary>
        /// Adds the specified number of hours to a Unix time in seconds, returning a new timestamp.
        /// </summary>
        /// <param name="unixSeconds">The Unix time in seconds.</param>
        /// <param name="hours">The number of hours to add (must be non-negative).</param>
        /// <returns>A new Unix time in seconds.</returns>
        public static long AddHours(this long unixSeconds, int hours)
        {
            if (hours < 0) throw new ArgumentOutOfRangeException(nameof(hours), "Hours must be non-negative.");
            return unixSeconds + (hours * 3600L);
        }

        /// <summary>
        /// Subtracts the specified number of hours from a Unix time in seconds, returning a new timestamp.
        /// </summary>
        /// <param name="unixSeconds">The Unix time in seconds.</param>
        /// <param name="hours">The number of hours to subtract (must be non-negative).</param>
        /// <returns>A new Unix time in seconds.</returns>
        public static long SubtractHours(this long unixSeconds, int hours)
        {
            if (hours < 0) throw new ArgumentOutOfRangeException(nameof(hours), "Hours must be non-negative.");
            return unixSeconds - (hours * 3600L);
        }

        /// <summary>
        /// Adds the specified number of minutes to a Unix time in seconds, returning a new timestamp.
        /// </summary>
        /// <param name="unixSeconds">The Unix time in seconds.</param>
        /// <param name="minutes">The number of minutes to add (must be non-negative).</param>
        /// <returns>A new Unix time in seconds.</returns>
        public static long AddMinutes(this long unixSeconds, int minutes)
        {
            if (minutes < 0) throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be non-negative.");
            return unixSeconds + (minutes * 60L);
        }

        /// <summary>
        /// Subtracts the specified number of minutes from a Unix time in seconds, returning a new timestamp.
        /// </summary>
        /// <param name="unixSeconds">The Unix time in seconds.</param>
        /// <param name="minutes">The number of minutes to subtract (must be non-negative).</param>
        /// <returns>A new Unix time in seconds.</returns>
        public static long SubtractMinutes(this long unixSeconds, int minutes)
        {
            if (minutes < 0) throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be non-negative.");
            return unixSeconds - (minutes * 60L);
        }

        /// <summary>
        /// Adds the specified number of seconds to a Unix time in seconds, returning a new timestamp.
        /// </summary>
        /// <param name="unixSeconds">The Unix time in seconds.</param>
        /// <param name="seconds">The number of seconds to add (must be non-negative).</param>
        /// <returns>A new Unix time in seconds.</returns>
        public static long AddSeconds(this long unixSeconds, int seconds)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds must be non-negative.");
            return unixSeconds + seconds;
        }

        /// <summary>
        /// Subtracts the specified number of seconds from a Unix time in seconds, returning a new timestamp.
        /// </summary>
        /// <param name="unixSeconds">The Unix time in seconds.</param>
        /// <param name="seconds">The number of seconds to subtract (must be non-negative).</param>
        /// <returns>A new Unix time in seconds.</returns>
        public static long SubtractSeconds(this long unixSeconds, int seconds)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds must be non-negative.");
            return unixSeconds - seconds;
        }
        /// <summary>
        /// Safely fire-and-forget an async task with optional exception handling.
        /// </summary>
        public static async void SafeFireAndForget(this Task task, bool continueOnCapturedContext = true, Action<Exception>? onException = null)
        {
            try
            {
                await task.ConfigureAwait(continueOnCapturedContext);
            }
            catch (Exception ex) when (onException != null)
            {
                onException(ex);
            }
        }

        /// <summary>
        /// Decomposes a UtcMilliTime interval into days, hours, minutes, and seconds.
        /// </summary>
        /// <returns>A struct with the decomposed parts.</returns>
        public static IntervalParts GetIntervalParts(this long interval)
        {
            int days = (int)(interval / Constants.day_milliseconds);
            long remainder = interval % Constants.day_milliseconds;
            int hours = (int)(remainder / Constants.hour_milliseconds);
            remainder %= Constants.hour_milliseconds;
            int minutes = (int)(remainder / Constants.minute_milliseconds);
            remainder %= Constants.minute_milliseconds;
            int seconds = (int)(remainder / Constants.second_milliseconds);
            return new IntervalParts(days, hours, minutes, seconds);
        }
        public readonly record struct IntervalParts(int Days, int Hours, int Minutes, int Seconds);
    }
#pragma warning restore RECS0165
    #endregion
    #region TestClock
    /// <summary>
    /// A fully controllable clock for unit testing.
    /// Time only advances when explicitly told to via Advance() methods.
    /// Non-time behavior matches the real Clock as closely as possible.
    /// </summary>
    public sealed class TestClock : ITime
    {
        private long _currentTime;
        private bool _suppressNetworkCalls = true;
        private readonly object _lock = new();

        public TestClock(long initialTime = 0)
        {
            _currentTime = initialTime;
        }

        /// <summary>
        /// Creates a TestClock initialized to the current real time.
        /// </summary>
        public static TestClock FromCurrentTime() => new(Clock.Time.Now);

        // ==================== Time Control ====================

        public void SetTime(long timestamp)
        {
            lock (_lock) { _currentTime = timestamp; }
        }

        public void Advance(TimeSpan time)
        {
            if (time < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(time), "Cannot advance by a negative amount.");

            lock (_lock) { _currentTime += (long)time.TotalMilliseconds; }
        }

        public void AdvanceMilliseconds(long milliseconds)
        {
            if (milliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Cannot advance by a negative amount.");

            lock (_lock) { _currentTime += milliseconds; }
        }

        public void AdvanceSeconds(int seconds) => AdvanceMilliseconds(seconds * 1000L);
        public void AdvanceMinutes(int minutes) => AdvanceMilliseconds(minutes * 60L * 1000L);
        public void AdvanceHours(int hours) => AdvanceMilliseconds(hours * 3600L * 1000L);

        /// <summary>
        /// Advances the clock by the specified number of days.
        /// </summary>
        public void AdvanceDays(int days)
        {
            if (days < 0)
                throw new ArgumentOutOfRangeException(nameof(days), "Cannot advance by a negative amount.");

            AdvanceMilliseconds(days * 24L * 60 * 60 * 1000);
        }

        /// <summary>
        /// Advances the clock by the specified number of weeks.
        /// </summary>
        public void AdvanceWeeks(int weeks)
        {
            if (weeks < 0)
                throw new ArgumentOutOfRangeException(nameof(weeks), "Cannot advance by a negative amount.");

            AdvanceDays(weeks * 7);
        }

        /// <summary>
        /// Advances the clock by the specified number of months (approximate).
        /// Uses 30 days per month for simplicity.
        /// </summary>
        public void AdvanceMonths(int months)
        {
            if (months < 0)
                throw new ArgumentOutOfRangeException(nameof(months), "Cannot advance by a negative amount.");

            AdvanceDays(months * 30);
        }

        /// <summary>
        /// Advances the clock by the specified number of years (approximate).
        /// Uses 365 days per year for simplicity.
        /// </summary>
        public void AdvanceYears(int years)
        {
            if (years < 0)
                throw new ArgumentOutOfRangeException(nameof(years), "Cannot advance by a negative amount.");

            AdvanceDays(years * 365);
        }

        // ==================== ITime Implementation ====================

        public long Now
        {
            get { lock (_lock) ; return _currentTime; }
        }

        public byte[] NowNonce()
        {
            long timestamp;
            lock (_lock) { timestamp = _currentTime; }

            byte[] nonce = new byte[12];

            // Timestamp portion (little-endian)
            for (int i = 0; i < 8; i++)
                nonce[i] = (byte)(timestamp >> (i * 8));

            // Deterministic entropy portion for test reproducibility
            int counter = (int)(timestamp & 0xFFFFFFFF);
            for (int i = 0; i < 4; i++)
                nonce[8 + i] = (byte)(counter >> (i * 8));

            return nonce;
        }

        // ==================== Behavioral Properties ====================

        public bool SuppressNetworkCalls
        {
            get => _suppressNetworkCalls;
            set => _suppressNetworkCalls = value;
        }

        public string DefaultServer { get; set; } = Constants.fallback_server;

        public bool Initialized => true;
        public bool Synchronized => true;
        public long Skew => 0;
        public long DeviceBootTime => 0;
        public long DeviceUpTime => 0;
        public long DeviceUtcNow => Now;

        public event EventHandler<NTPEventArgs>? NetworkTimeAcquired;

        public Task SelfUpdateAsync(string ntpServerHostName = Constants.fallback_server)
        {
            // Intentionally a no-op to avoid real network calls during tests
            return Task.CompletedTask;
        }

        // ==================== Test Helper ====================

        /// <summary>
        /// Manually raises the NetworkTimeAcquired event.
        /// Useful for testing subscribers to this event.
        /// </summary>
        /// <param name="server">The NTP server name.</param>
        /// <param name="latency">Round-trip latency in milliseconds.</param>
        /// <param name="skew">Clock skew in milliseconds.</param>
        public void RaiseNetworkTimeAcquired(string server, long latency = 0, long skew = 0)
        {
            var args = new NTPEventArgs(server, latency, skew);
            NetworkTimeAcquired?.Invoke(this, args);
        }

        /// <summary>
        /// Raises the NetworkTimeAcquired event using a pre-constructed NTPEventArgs instance.
        /// </summary>
        public void RaiseNetworkTimeAcquired(NTPEventArgs args)
        {
            NetworkTimeAcquired?.Invoke(this, args);
        }
    }
    #endregion
}