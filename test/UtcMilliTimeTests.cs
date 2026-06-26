using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Threading;
using UtcMilliTime;
using Xunit;

namespace UtcMilliTime.Tests
{
    public class UtcMilliTimeTests
    {
        #region NowNonce Tests

        [Fact]
        public void NowNonce_Returns12ByteArray()
        {
            byte[] nonce = Clock.Time.NowNonce();
            Assert.Equal(12, nonce.Length);
        }

        [Fact]
        public void TimestampFromNowNonce_ShouldReturnCorrectTimestamp()
        {
            long expectedTimestamp = 1_700_000_000_000;
            byte[] nonce = new byte[12];
            BinaryPrimitives.WriteInt64BigEndian(nonce, expectedTimestamp);

            long actual = Clock.Time.TimestampFromNowNonce(nonce);
            Assert.Equal(expectedTimestamp, actual);
        }

        [Fact]
        public void EntropyFromNowNonce_ShouldReturnCorrectEntropy()
        {
            uint expectedEntropy = 0x12345678;
            byte[] nonce = new byte[12];
            BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8), expectedEntropy);

            uint actual = Clock.Time.EntropyFromNowNonce(nonce);
            Assert.Equal(expectedEntropy, actual);
        }

        [Fact]
        public void TimestampFromNowNonce_InvalidLength_Throws()
        {
            byte[] invalid = new byte[10];
            Assert.Throws<ArgumentException>(() => Clock.Time.TimestampFromNowNonce(invalid));
        }

        [Fact]
        public void EntropyFromNowNonce_InvalidLength_Throws()
        {
            byte[] invalid = new byte[10];
            Assert.Throws<ArgumentException>(() => Clock.Time.EntropyFromNowNonce(invalid));
        }

        #endregion

        #region ISO-8601 Conversion

        [Fact]
        public void ToIso8601String_WithMilliseconds_ReturnsCorrectFormat()
        {
            long timestamp = 1_700_000_000_123;
            string result = timestamp.ToIso8601String();
            Assert.Equal("2023-11-14T22:13:20.123Z", result);
        }

        [Fact]
        public void ToIso8601String_WithoutMilliseconds_OmitsMilliseconds()
        {
            long timestamp = 1_700_000_000_123;
            string result = timestamp.ToIso8601String(suppressMilliseconds: true);
            Assert.Equal("2023-11-14T22:13:20Z", result);
        }

        #endregion

        #region Unix Time Conversions

        [Fact]
        public void ToUnixTime_DividesBy1000()
        {
            long timestamp = 1_700_000_000_500;
            Assert.Equal(1_700_000_000, timestamp.ToUnixTime());
        }

        [Fact]
        public void ToUnixTimeSeconds_IsAliasForToUnixTime()
        {
            long timestamp = 1_700_000_000_500;
            Assert.Equal(timestamp.ToUnixTime(), timestamp.ToUnixTimeSeconds());
        }

        [Fact]
        public void ToUtcMilliTime_FromUnixSeconds_MultipliesBy1000()
        {
            long unixSeconds = 1_700_000_000;
            Assert.Equal(1_700_000_000_000, unixSeconds.ToUtcMilliTime());
        }

        #endregion

        #region DateTime / DateTimeOffset / TimeSpan Conversions

        [Fact]
        public void ToUtcDateTime_ReturnsCorrectDateTime()
        {
            long timestamp = 0;
            DateTime result = timestamp.ToUtcDateTime();
            Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), result);
        }

        [Fact]
        public void ToLocalDateTime_ReturnsLocalKind()
        {
            long timestamp = 0;
            DateTime result = timestamp.ToLocalDateTime();
            Assert.NotEqual(DateTimeKind.Utc, result.Kind);
        }

        [Fact]
        public void ToDateTimeOffset_ReturnsUtcOffset()
        {
            long timestamp = 1_700_000_000_000;
            DateTimeOffset result = timestamp.ToDateTimeOffset();
            Assert.Equal(TimeSpan.Zero, result.Offset);
        }

        [Fact]
        public void ToTimeSpan_ReturnsCorrectTimeSpan()
        {
            long interval = 86_400_000; // 1 day
            Assert.Equal(TimeSpan.FromDays(1), interval.ToTimeSpan());
        }

        #endregion

        #region Millisecond Part

        [Fact]
        public void MillisecondPart_ReturnsCorrectValue()
        {
            long timestamp = 1_700_000_000_456;
            Assert.Equal(456, timestamp.MillisecondPart());
        }

        #endregion

        #region Interval Decomposition

        [Fact]
        public void IntervalDays_ReturnsCorrectDays()
        {
            long interval = 2 * 86_400_000 + 3600_000;
            Assert.Equal(2, interval.IntervalDays());
        }

        [Fact]
        public void IntervalHoursPart_ReturnsCorrectHours()
        {
            long interval = 2 * 86_400_000 + 5 * 3600_000;
            Assert.Equal(5, interval.IntervalHoursPart());
        }

        [Fact]
        public void IntervalMinutesPart_ReturnsCorrectMinutes()
        {
            long interval = 90 * 60_000;
            Assert.Equal(30, interval.IntervalMinutesPart());
        }

        [Fact]
        public void IntervalSecondsPart_ReturnsCorrectSeconds()
        {
            long interval = 125_000;
            Assert.Equal(5, interval.IntervalSecondsPart());
        }

        [Fact]
        public void GetIntervalParts_ReturnsCorrectValues()
        {
            long interval = (2 * 86_400_000) + (5 * 3600_000) + (30 * 60_000) + 15000;
            var parts = interval.GetIntervalParts();

            Assert.Equal(2, parts.Days);
            Assert.Equal(5, parts.Hours);
            Assert.Equal(30, parts.Minutes);
            Assert.Equal(15, parts.Seconds);
        }

        #endregion

        #region Additive / Subtractive Methods

        [Theory]
        [InlineData(0, 1, 86400)]
        [InlineData(100000, 5, 532000)]
        public void AddDays_ReturnsCorrectValue(long unixSeconds, int days, long expected)
        {
            Assert.Equal(expected, unixSeconds.AddDays(days));
        }

        [Fact]
        public void AddDays_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => 0L.AddDays(-1));
        }

        [Theory]
        [InlineData(0, 1, 3600)]
        [InlineData(100000, 2, 107200)]
        public void AddHours_ReturnsCorrectValue(long unixSeconds, int hours, long expected)
        {
            Assert.Equal(expected, unixSeconds.AddHours(hours));
        }

        [Fact]
        public void AddHours_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => 0L.AddHours(-1));
        }

        [Theory]
        [InlineData(0, 1, 60)]
        [InlineData(100000, 90, 105400)]
        public void AddMinutes_ReturnsCorrectValue(long unixSeconds, int minutes, long expected)
        {
            Assert.Equal(expected, unixSeconds.AddMinutes(minutes));
        }

        [Fact]
        public void AddMinutes_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => 0L.AddMinutes(-1));
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(100000, 30, 100030)]
        public void AddSeconds_ReturnsCorrectValue(long unixSeconds, int seconds, long expected)
        {
            Assert.Equal(expected, unixSeconds.AddSeconds(seconds));
        }

        [Fact]
        public void AddSeconds_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => 0L.AddSeconds(-1));
        }

        [Fact]
        public void SubtractDays_ReturnsCorrectValue()
        {
            Assert.Equal(0, 86400L.SubtractDays(1));
        }

        [Fact]
        public void SubtractDays_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => 0L.SubtractDays(-1));
        }

        #endregion

        #region TestClock Tests

        public class TestClockTests
        {
            [Fact]
            public void TestClock_DefaultConstructor_StartsAtZero()
            {
                var clock = new TestClock();
                Assert.Equal(0, clock.Now);
            }

            [Fact]
            public void TestClock_Constructor_SetsInitialTime()
            {
                var clock = new TestClock(1_700_000_000_000);
                Assert.Equal(1_700_000_000_000, clock.Now);
            }

            [Fact]
            public void TestClock_FromCurrentTime_UsesRealTime()
            {
                long before = Clock.Time.Now;
                var clock = TestClock.FromCurrentTime();
                long after = Clock.Time.Now;

                Assert.True(clock.Now >= before - 2000 && clock.Now <= after + 2000);
            }

            [Fact]
            public void TestClock_SetTime_UpdatesNow()
            {
                var clock = new TestClock();
                clock.SetTime(1_234_567_890_000);
                Assert.Equal(1_234_567_890_000, clock.Now);
            }

            [Fact]
            public void TestClock_AdvanceMilliseconds_IncreasesTimeCorrectly()
            {
                var clock = new TestClock(1_000_000_000_000);
                clock.AdvanceMilliseconds(7500);
                Assert.Equal(1_000_000_007_500, clock.Now);
            }

            [Fact]
            public void TestClock_Advance_WithTimeSpan_Works()
            {
                var clock = new TestClock(1_000_000_000_000);
                clock.Advance(TimeSpan.FromMinutes(3));
                Assert.Equal(1_000_000_000_000 + 3 * 60 * 1000, clock.Now);
            }

            [Fact]
            public void TestClock_AdvanceSeconds_Minutes_Hours_Work()
            {
                var clock = new TestClock(1_000_000_000_000);

                clock.AdvanceSeconds(45);
                Assert.Equal(1_000_000_000_000 + 45_000, clock.Now);

                clock.AdvanceMinutes(2);
                Assert.Equal(1_000_000_000_000 + 45_000 + 120_000, clock.Now);

                clock.AdvanceHours(1);
                Assert.Equal(1_000_000_000_000 + 45_000 + 120_000 + 3_600_000, clock.Now);
            }

            [Fact]
            public void TestClock_AdvanceDays_Works()
            {
                var clock = new TestClock(1_700_000_000_000_000);
                long delta = 5L * 24 * 60 * 60 * 1000;
                clock.AdvanceDays(5);
                Assert.Equal(1_700_000_000_000_000 + delta, clock.Now);
            }

            [Fact]
            public void TestClock_AdvanceWeeks_Works()
            {
                var clock = new TestClock(1_700_000_000_000_000);
                clock.AdvanceWeeks(3);

                long expectedDelta = 3L * 7 * 24 * 60 * 60 * 1000;
                Assert.Equal(1_700_000_000_000_000 + expectedDelta, clock.Now);
            }

            [Fact]
            public void TestClock_AdvanceMonths_UsesApproximate30Days()
            {
                var clock = new TestClock(1_700_000_000_000_000);
                long delta = 2L * 30 * 24 * 60 * 60 * 1000;
                clock.AdvanceMonths(2);
                Assert.Equal(1_700_000_000_000_000 + delta, clock.Now);
            }

            [Fact]
            public void TestClock_AdvanceYears_UsesApproximate365Days()
            {
                var clock = new TestClock(1_700_000_000_000_000);
                long delta = 1L * 365 * 24 * 60 * 60 * 1000;
                clock.AdvanceYears(1);
                Assert.Equal(1_700_000_000_000_000 + delta, clock.Now);
            }
            [Fact]
            public void TestClock_AdvanceDays_Months_Years_Negative_Throws()
            {
                var clock = new TestClock();

                Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceDays(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceMonths(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceYears(-1));
            }

            [Fact]
            public void TestClock_Advance_NegativeValue_ThrowsArgumentOutOfRangeException()
            {
                var clock = new TestClock();

                Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceMilliseconds(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromSeconds(-5)));
            }

            [Fact]
            public void TestClock_Now_DoesNotChangeWithoutExplicitAdvance()
            {
                var clock = new TestClock(5_000_000_000_000);
                long first = clock.Now;
                Thread.Sleep(30);
                long second = clock.Now;

                Assert.Equal(first, second);
            }

            [Fact]
            public void TestClock_NowNonce_IsStable_WhenTimeDoesNotChange()
            {
                var clock = new TestClock(9_000_000_000_000);
                byte[] nonce1 = clock.NowNonce();
                byte[] nonce2 = clock.NowNonce();

                Assert.Equal(nonce1, nonce2);
            }

            [Fact]
            public void TestClock_NowNonce_Changes_AfterTimeAdvance()
            {
                var clock = new TestClock(9_000_000_000_000);
                byte[] before = clock.NowNonce();

                clock.AdvanceMilliseconds(100);
                byte[] after = clock.NowNonce();

                Assert.NotEqual(before, after);
            }

            [Fact]
            public void TestClock_SuppressNetworkCalls_DefaultsToTrue_AndIsMutable()
            {
                var clock = new TestClock();
                Assert.True(clock.SuppressNetworkCalls);

                clock.SuppressNetworkCalls = false;
                Assert.False(clock.SuppressNetworkCalls);
            }

            [Fact]
            public void TestClock_RaiseNetworkTimeAcquired_RaisesEventWithCorrectData()
            {
                var clock = new TestClock();
                NTPEventArgs? captured = null;

                clock.NetworkTimeAcquired += (_, args) => captured = args;

                clock.RaiseNetworkTimeAcquired("ntp.example.com", skew: 22, latency: 67);

                Assert.NotNull(captured);
                Assert.Equal("ntp.example.com", captured.Server);
                Assert.Equal(22, captured.Skew);
                Assert.Equal(67, captured.Latency);
            }

            [Fact]
            public void TestClock_RaiseNetworkTimeAcquired_AcceptsCustomArgs()
            {
                var clock = new TestClock();
                var args = new NTPEventArgs("time.cloudflare.com", 5, 33);
                NTPEventArgs? received = null;

                clock.NetworkTimeAcquired += (_, e) => received = e;

                clock.RaiseNetworkTimeAcquired(args);

                Assert.NotNull(received);
                Assert.Equal("time.cloudflare.com", received.Server);
            }
        }

        #endregion
    }
}