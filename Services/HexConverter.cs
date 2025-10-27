using System;
using System.Globalization;
using System.Text;

namespace CustomHidChecker.Services
{
    public static class HexConverter
    {
        public static string ToHexString(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(data.Length * 3);
            for (var i = 0; i < data.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static bool TryParseHexString(string input, Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return true;
            }

            var span = input.AsSpan().Trim();
            var buffer = new Span<byte>(new byte[(span.Length / 2) + 1]);
            var index = 0;
            var temp = new Span<char>(new char[2]);
            var hasHighNibble = false;

            for (var i = 0; i < span.Length; i++)
            {
                var c = span[i];
                if (char.IsWhiteSpace(c))
                {
                    if (hasHighNibble)
                    {
                        return false;
                    }

                    continue;
                }

                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }

                temp[index % 2] = c;
                hasHighNibble = !hasHighNibble;
                if (!hasHighNibble)
                {
                    buffer[index / 2] = byte.Parse(temp, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    index += 2;
                }
            }

            if (hasHighNibble)
            {
                temp[1] = '0';
                buffer[index / 2] = byte.Parse(temp, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                index += 2;
            }

            var length = index / 2;
            if (length > destination.Length)
            {
                return false;
            }

            buffer[..length].CopyTo(destination);
            bytesWritten = length;
            return true;
        }
    }
}
