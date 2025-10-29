using CustomHidChecker.Interfaces;
using CustomHidChecker.Models;

namespace CustomHidChecker.Services
{
    public sealed class HidDeviceSession : IDisposable
    {
        private readonly Func<IHidClassDescriptor> _deviceFactory;
        private IHidClassDescriptor? _device;
        private CancellationTokenSource? _readLoopCts;

        public HidDeviceSession(Func<IHidClassDescriptor> deviceFactory)
        {
            _deviceFactory = deviceFactory;
        }

        public event EventHandler<byte[]>? InputReportReceived;

        public event EventHandler<string>? ReadErrorOccurred;

        public bool IsConnected => _device != null;

        public string? LastError => _device?.LastErrorMessage;

        public async Task<SessionConnectionResult> ConnectAsync(HidDeviceInfo deviceInfo, CancellationToken cancellationToken)
        {
            if (_device != null)
            {
                return SessionConnectionResult.Successful();
            }

            var device = _deviceFactory();
            bool opened;
            try
            {
                opened = await Task.Run(() => device.Open(deviceInfo.DevicePath), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                device.Dispose();
                return SessionConnectionResult.Failed(ex.Message);
            }

            if (!opened)
            {
                var error = device.LastErrorMessage ?? "不明な理由で接続に失敗";
                device.Dispose();
                return SessionConnectionResult.Failed(error);
            }

            _device = device;
            _readLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = Task.Run(() => ReadLoopAsync(deviceInfo.InputReportLength, _readLoopCts.Token));

            return SessionConnectionResult.Successful();
        }

        public Task<bool> WriteOutputAsync(byte[] report, CancellationToken cancellationToken)
        {
            return _device?.WriteAsync(report, cancellationToken) ?? Task.FromResult(false);
        }

        public bool TryGetFeature(byte[] buffer, out string? errorMessage)
        {
            if (_device is null)
            {
                errorMessage = "デバイス未接続";
                return false;
            }

            if (!_device.GetFeature(buffer))
            {
                errorMessage = _device.LastErrorMessage ?? "Feature Report取得に失敗";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public bool TrySetFeature(byte[] report, out string? errorMessage)
        {
            if (_device is null)
            {
                errorMessage = "デバイス未接続";
                return false;
            }

            if (!_device.SetFeature(report))
            {
                errorMessage = _device.LastErrorMessage ?? "Feature Report送信に失敗";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public void Disconnect()
        {
            _readLoopCts?.Cancel();
            _readLoopCts?.Dispose();
            _readLoopCts = null;

            if (_device != null)
            {
                _device.Close();
                _device.Dispose();
                _device = null;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        private async Task ReadLoopAsync(int reportLength, CancellationToken cancellationToken)
        {
            if (_device is null)
            {
                return;
            }

            var device = _device;
            var length = reportLength > 0 ? reportLength : 64;
            var buffer = new byte[length];

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var memory = buffer.AsMemory();
                    var read = await device.ReadAsync(memory, 1000, cancellationToken).ConfigureAwait(false);
                    if (read > 0)
                    {
                        var data = memory.Slice(0, read).ToArray();
                        InputReportReceived?.Invoke(this, data);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ReadErrorOccurred?.Invoke(this, ex.Message);
                    break;
                }
            }
        }
    }

    public readonly struct SessionConnectionResult
    {
        private SessionConnectionResult(bool success, string? message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }

        public string? Message { get; }

        public static SessionConnectionResult Successful()
        {
            return new SessionConnectionResult(true, null);
        }

        public static SessionConnectionResult Failed(string message)
        {
            return new SessionConnectionResult(false, message);
        }
    }
}
