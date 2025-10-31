using System;
using CustomHidChecker.Services;
using Xunit;

namespace CustomHidChecker.Tests
{
    public class HexConverterTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void TryParse_EmptyOrWhitespace_Succeeds(string input)
        {
            var dest = new byte[4];
            var ok = HexConverter.TryParseHexString(input, dest, out var written);
            Assert.True(ok);
            Assert.Equal(0, written);
        }

        [Fact]
        public void TryParse_DecimalOnly_Succeeds()
        {
            var dest = new byte[3];
            var ok = HexConverter.TryParseHexString("0 15 255", dest, out var written);
            Assert.True(ok);
            Assert.Equal(3, written);
            Assert.Equal(new byte[] { 0x00, 0x0F, 0xFF }, dest);
        }

        [Fact]
        public void TryParse_HexOnly_Succeeds()
        {
            var dest = new byte[3];
            var ok = HexConverter.TryParseHexString("0x00 0X1A 0xFF", dest, out var written);
            Assert.True(ok);
            Assert.Equal(3, written);
            Assert.Equal(new byte[] { 0x00, 0x1A, 0xFF }, dest);
        }

        [Fact]
        public void TryParse_Mixed_Succeeds()
        {
            var dest = new byte[4];
            var ok = HexConverter.TryParseHexString("15 0x1A 255 0X00", dest, out var written);
            Assert.True(ok);
            Assert.Equal(4, written);
            Assert.Equal(new byte[] { 15, 26, 255, 0 }, dest);
        }

        [Fact]
        public void TryParse_WhitespaceVariants_Succeeds()
        {
            var dest = new byte[3];
            var ok = HexConverter.TryParseHexString("  1\t0x02\n3  ", dest, out var written);
            Assert.True(ok);
            Assert.Equal(3, written);
            Assert.Equal(new byte[] { 1, 2, 3 }, dest);
        }

        [Theory]
        [InlineData("256")] // decimal out of range
        [InlineData("0xGG")] // invalid hex
        [InlineData("10,0x1A")] // invalid token due to comma
        public void TryParse_InvalidInputs_Fail(string input)
        {
            var dest = new byte[8];
            var ok = HexConverter.TryParseHexString(input, dest, out var written);
            Assert.False(ok);
            Assert.Equal(0, written);
        }

        [Fact]
        public void TryParse_CapacityExceeded_Fail()
        {
            var dest = new byte[2];
            var ok = HexConverter.TryParseHexString("01 02 03", dest, out var written);
            Assert.False(ok);
            Assert.Equal(0, written);
        }
    }
}
