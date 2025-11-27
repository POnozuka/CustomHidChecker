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
            var buffer = new Span<byte>(new byte[((span.Length + 1) / 2)]);
            var byteCount = 0;

            var i = 0;
            while (i < span.Length)
            {
                while (i < span.Length && char.IsWhiteSpace(span[i])) i++;
                if (i >= span.Length) break;

                var start = i;
                while (i < span.Length && !char.IsWhiteSpace(span[i])) i++;
                var token = span[start..i];
                if (token.IsEmpty)
                {
                    continue;
                }

                if (!byte.TryParse(token, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                {
                    return false;
                }

                if (byteCount >= buffer.Length)
                {
                    return false;
                }
                buffer[byteCount++] = value;
            }

            if (byteCount > destination.Length)
            {
                return false;
            }

            buffer[..byteCount].CopyTo(destination);
            bytesWritten = byteCount;
            return true;
        }
    }
}
