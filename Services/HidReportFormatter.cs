using CustomHidChecker.Services;

namespace CustomHidChecker.Services
{
    public sealed class HidReportFormatter
    {
        public bool TryCreateReport(string input, int length, out byte[]? report, out string? errorMessage)
        {
            var effectiveLength = NormalizeLength(length);
            var buffer = new byte[effectiveLength];
            if (!HexConverter.TryParseHexString(input, buffer, out var written))
            {
                errorMessage = "HEX形式が不正です";
                report = null;
                return false;
            }

            if (written == 0)
            {
                errorMessage = "Report IDを先頭に指定してください";
                report = null;
                return false;
            }

            report = buffer;
            errorMessage = null;
            return true;
        }

        public int NormalizeLength(int length)
        {
            return length > 0 ? length : 64;
        }
    }
}
