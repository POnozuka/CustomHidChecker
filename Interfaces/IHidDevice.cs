using System;
using System.Threading;
using System.Threading.Tasks;

namespace CustomHidChecker.Interfaces
{
    public interface IHidDevice : IDisposable
    {
        bool IsOpen { get; }

        string? LastErrorMessage { get; }

        bool Open(string devicePath);

        void Close();

        Task<bool> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

        Task<int> ReadAsync(Memory<byte> buffer, int timeoutMilliseconds, CancellationToken cancellationToken);

        bool GetFeature(Span<byte> buffer);

        bool SetFeature(ReadOnlySpan<byte> buffer);
    }
}
