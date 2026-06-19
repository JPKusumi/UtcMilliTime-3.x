using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
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
    }
}