using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CustomHidChecker.Interfaces;
using Microsoft.Win32.SafeHandles;

namespace CustomHidChecker.Services
{
    public sealed class WinHidDevice : IHidDevice
    {
        private SafeFileHandle? _handle;
        private FileStream? _readStream;
        private FileStream? _writeStream;
        private bool _disposed;

        public string? LastErrorMessage { get; private set; }

        public bool IsOpen => _handle is { IsInvalid: false } && !_handle.IsClosed;

        public bool Open(string devicePath)
        {
            if (IsOpen)
            {
                return true;
            }

            try
            {
                _handle = HidNativeMethods.CreateFileForReadWrite(devicePath);
                _readStream = new FileStream(_handle, FileAccess.Read, 4096, true);
                _writeStream = new FileStream(_handle, FileAccess.Write, 4096, true);
                LastErrorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                Close();
                return false;
            }
        }

        public void Close()
        {
            if (_disposed)
            {
                return;
            }

            _readStream?.Dispose();
            _writeStream?.Dispose();
            _handle?.Dispose();
            _readStream = null;
            _writeStream = null;
            _handle = null;
        }

        public async Task<bool> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (_writeStream is null)
            {
                LastErrorMessage = "Write stream is not available.";
                return false;
            }

            try
            {
                await _writeStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                await _writeStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                LastErrorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                return false;
            }
        }

        public async Task<int> ReadAsync(Memory<byte> buffer, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            if (_readStream is null)
            {
                LastErrorMessage = "Read stream is not available.";
                return 0;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMilliseconds > 0)
            {
                timeoutCts.CancelAfter(timeoutMilliseconds);
            }

            try
            {
                return await _readStream.ReadAsync(buffer, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                LastErrorMessage = null;
                return 0;
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                return 0;
            }
        }

        public bool GetFeature(Span<byte> buffer)
        {
            var handle = _handle;
            if (handle is null)
            {
                LastErrorMessage = "Device handle is not available.";
                return false;
            }

            var managedBuffer = buffer.ToArray();
            if (!HidNativeMethods.HidD_GetFeature(handle, managedBuffer, managedBuffer.Length))
            {
                LastErrorMessage = HidNativeMethods.GetLastErrorMessage();
                return false;
            }

            managedBuffer.CopyTo(buffer);
            LastErrorMessage = null;
            return true;
        }

        public bool SetFeature(ReadOnlySpan<byte> buffer)
        {
            var handle = _handle;
            if (handle is null)
            {
                LastErrorMessage = "Device handle is not available.";
                return false;
            }

            var managed = buffer.ToArray();
            var result = HidNativeMethods.HidD_SetFeature(handle, managed, managed.Length);
            LastErrorMessage = result ? null : HidNativeMethods.GetLastErrorMessage();
            return result;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Close();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
