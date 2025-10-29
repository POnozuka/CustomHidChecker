using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CustomHidChecker.Commands;
using CustomHidChecker.Interfaces;
using CustomHidChecker.Models;
using CustomHidChecker.Services;
using Microsoft.Win32;

namespace CustomHidChecker.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly ObservableCollection<HidDeviceInfo> _devices;
        private readonly Func<IHidClassDescriptor> _deviceFactory;
        private readonly DeviceDiscoveryService _deviceDiscovery;
        private readonly DeviceLogService _logService;
        private readonly HidReportFormatter _reportFormatter;
        private readonly HidDeviceSession _session;
        private readonly UsbDescriptorService _descriptorService;
        private HidDeviceInfo? _selectedDevice;
        private string _outputReportText = string.Empty;
        private string _featureReportText = string.Empty;
        private string _statusMessage = "準備完了";
        private string _connectedDeviceDetails = "未接続";
        private string _lastInputReport = "未受信";
        private string _lastInputReportTimestamp = "-";
        private UsbDescriptorInfo _descriptorInfo = UsbDescriptorInfo.Empty;
        private bool _isConnected;
        private bool _isBusy;

        public MainViewModel()
            : this(() => new WinHidDevice())
        {
        }

        public MainViewModel(Func<IHidClassDescriptor> deviceFactory)
        {
            _deviceFactory = deviceFactory;
            _devices = new ObservableCollection<HidDeviceInfo>();
            _deviceDiscovery = new DeviceDiscoveryService();
            _logService = new DeviceLogService();
            _reportFormatter = new HidReportFormatter();
            _session = new HidDeviceSession(_deviceFactory);
            _descriptorService = new UsbDescriptorService();
            _session.InputReportReceived += OnInputReportReceived;
            _session.ReadErrorOccurred += OnReadErrorOccurred;

            RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices(), _ => !IsBusy);
            ConnectCommand = new RelayCommand(_ => ConnectDevice(), _ => !IsBusy && !IsConnected && SelectedDevice != null);
            DisconnectCommand = new RelayCommand(_ => DisconnectDevice(), _ => IsConnected);
            SendOutputCommand = new RelayCommand(_ => SendOutputReport(), _ => IsConnected);
            GetFeatureCommand = new RelayCommand(_ => GetFeatureReport(), _ => IsConnected);
            SetFeatureCommand = new RelayCommand(_ => SetFeatureReport(), _ => IsConnected);
            ExportLogCommand = new RelayCommand(_ => ExportLog(), _ => _logService.HasEntries);
            ClearLogCommand = new RelayCommand(_ => ClearLog(), _ => _logService.HasEntries);

            RefreshDevices();
        }

        public ObservableCollection<HidDeviceInfo> Devices => _devices;

        public ObservableCollection<LogEntryViewModel> LogEntries => _logService.Entries;

        public HidDeviceInfo? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public string OutputReportText
        {
            get => _outputReportText;
            set => SetProperty(ref _outputReportText, value);
        }

        public string FeatureReportText
        {
            get => _featureReportText;
            set => SetProperty(ref _featureReportText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ConnectedDeviceDetails
        {
            get => _connectedDeviceDetails;
            private set => SetProperty(ref _connectedDeviceDetails, value);
        }

        public string LastInputReport
        {
            get => _lastInputReport;
            private set => SetProperty(ref _lastInputReport, value);
        }

        public string LastInputReportTimestamp
        {
            get => _lastInputReportTimestamp;
            private set => SetProperty(ref _lastInputReportTimestamp, value);
        }

        public UsbDescriptorInfo DescriptorInfo
        {
            get => _descriptorInfo;
            private set => SetProperty(ref _descriptorInfo, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public RelayCommand RefreshDevicesCommand { get; }

        public RelayCommand ConnectCommand { get; }

        public RelayCommand DisconnectCommand { get; }

        public RelayCommand SendOutputCommand { get; }

        public RelayCommand GetFeatureCommand { get; }

        public RelayCommand SetFeatureCommand { get; }

        public RelayCommand ExportLogCommand { get; }

        public RelayCommand ClearLogCommand { get; }

        private async void RefreshDevices()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "デバイスを検索しています...";
                var devices = await _deviceDiscovery.EnumerateAsync().ConfigureAwait(true);
                _devices.Clear();
                foreach (var device in devices)
                {
                    _devices.Add(device);
                }

                if (_devices.Count > 0)
                {
                    SelectedDevice = _devices[0];
                    StatusMessage = $"{_devices.Count} 台のデバイスを検出";
                }
                else
                {
                    SelectedDevice = null;
                    StatusMessage = "HIDデバイスが見つかりません";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"列挙に失敗: {ex.Message}";
                AddLog(DeviceLogDirection.Error, "デバイス列挙エラー", null);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ConnectDevice()
        {
            if (IsConnected || SelectedDevice == null)
            {
                return;
            }

            var deviceInfo = SelectedDevice;
            try
            {
                IsBusy = true;
                StatusMessage = "接続中...";
                var result = await _session.ConnectAsync(deviceInfo, CancellationToken.None).ConfigureAwait(true);
                if (!result.Success)
                {
                    StatusMessage = result.Message ?? "接続に失敗";
                    AddLog(DeviceLogDirection.Error, StatusMessage);
                    return;
                }

                IsConnected = true;
                StatusMessage = $"接続しました: {deviceInfo.DisplayName}";
                AddLog(DeviceLogDirection.Info, "デバイスに接続", null);
                ConnectedDeviceDetails = BuildDeviceDetails(deviceInfo);
                DescriptorInfo = _descriptorService.GetDescriptors(deviceInfo);
            }
            catch (Exception ex)
            {
                StatusMessage = $"接続に失敗: {ex.Message}";
                AddLog(DeviceLogDirection.Error, ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void DisconnectDevice()
        {
            _session.Disconnect();

            if (IsConnected)
            {
                AddLog(DeviceLogDirection.Info, "デバイスから切断", null);
            }

            IsConnected = false;
            StatusMessage = "未接続";
            ConnectedDeviceDetails = "未接続";
            LastInputReport = "未受信";
            LastInputReportTimestamp = "-";
            DescriptorInfo = UsbDescriptorInfo.Empty;
        }

        private void SendOutputReport()
        {
            if (!IsConnected || SelectedDevice is null)
            {
                return;
            }

            var deviceInfo = SelectedDevice;
            var length = _reportFormatter.NormalizeLength(deviceInfo.OutputReportLength);
            if (!_reportFormatter.TryCreateReport(OutputReportText, length, out var report, out var errorMessage))
            {
                StatusMessage = errorMessage ?? "出力データが不正";
                AddLog(DeviceLogDirection.Error, StatusMessage);
                return;
            }

            _ = Task.Run(async () =>
            {
                var success = await _session.WriteOutputAsync(report!, CancellationToken.None).ConfigureAwait(false);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (success)
                    {
                        StatusMessage = "Output Report送信完了";
                        AddLog(DeviceLogDirection.Output, "Output Report送信", report);
                    }
                    else
                    {
                        var message = _session.LastError ?? "送信に失敗";
                        StatusMessage = message;
                        AddLog(DeviceLogDirection.Error, message);
                    }
                });
            });
        }

        private void GetFeatureReport()
        {
            if (!IsConnected || SelectedDevice is null)
            {
                return;
            }

            var deviceInfo = SelectedDevice;
            var length = _reportFormatter.NormalizeLength(deviceInfo.FeatureReportLength);
            if (!_reportFormatter.TryCreateReport(FeatureReportText, length, out var buffer, out var errorMessage))
            {
                StatusMessage = errorMessage ?? "Feature Reportが不正";
                AddLog(DeviceLogDirection.Error, StatusMessage);
                return;
            }

            if (!_session.TryGetFeature(buffer!, out var getError))
            {
                StatusMessage = getError ?? "Feature Report取得に失敗";
                AddLog(DeviceLogDirection.Error, StatusMessage);
                return;
            }

            FeatureReportText = HexConverter.ToHexString(buffer!);
            StatusMessage = "Feature Report取得完了";
            AddLog(DeviceLogDirection.FeatureIn, "Feature Report受信", buffer);
        }

        private void SetFeatureReport()
        {
            if (!IsConnected || SelectedDevice is null)
            {
                return;
            }

            var deviceInfo = SelectedDevice;
            var length = _reportFormatter.NormalizeLength(deviceInfo.FeatureReportLength);
            if (!_reportFormatter.TryCreateReport(FeatureReportText, length, out var report, out var errorMessage))
            {
                StatusMessage = errorMessage ?? "Feature Reportが不正";
                AddLog(DeviceLogDirection.Error, StatusMessage);
                return;
            }

            if (_session.TrySetFeature(report!, out var setError))
            {
                StatusMessage = "Feature Report送信完了";
                AddLog(DeviceLogDirection.FeatureOut, "Feature Report送信", report);
            }
            else
            {
                StatusMessage = setError ?? "Feature Report送信に失敗";
                AddLog(DeviceLogDirection.Error, StatusMessage);
            }
        }

        private void ExportLog()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV ファイル (*.csv)|*.csv|テキスト ファイル (*.txt)|*.txt",
                FileName = $"HidLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                if (_logService.TryExport(dialog.FileName, out var error))
                {
                    StatusMessage = "ログをエクスポートしました";
                }
                else
                {
                    StatusMessage = $"エクスポートに失敗: {error}";
                    AddLog(DeviceLogDirection.Error, StatusMessage);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"エクスポートに失敗: {ex.Message}";
                AddLog(DeviceLogDirection.Error, ex.Message);
            }
        }

        private void ClearLog()
        {
            _logService.Clear();
            StatusMessage = "ログをクリアしました";
            RaiseCommandStates();
        }

        private void AddLog(DeviceLogDirection direction, string message, byte[]? payload = null)
        {
            _logService.Add(direction, message, payload);
            RaiseCommandStates();
        }

        private void RaiseCommandStates()
        {
            RefreshDevicesCommand.RaiseCanExecuteChanged();
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
            SendOutputCommand.RaiseCanExecuteChanged();
            GetFeatureCommand.RaiseCanExecuteChanged();
            SetFeatureCommand.RaiseCanExecuteChanged();
            ExportLogCommand.RaiseCanExecuteChanged();
            ClearLogCommand.RaiseCanExecuteChanged();
        }

        private void OnInputReportReceived(object? sender, byte[] data)
        {
            AddLog(DeviceLogDirection.Input, "Input Report受信", data);
            UpdateLastInputReport(data);
        }

        private void OnReadErrorOccurred(object? sender, string message)
        {
            StatusMessage = $"読み取りエラー: {message}";
            AddLog(DeviceLogDirection.Error, message);
            Application.Current.Dispatcher.Invoke(DisconnectDevice);
        }

        private static string BuildDeviceDetails(HidDeviceInfo info)
        {
            var inputLength = info.InputReportLength > 0 ? info.InputReportLength.ToString() : "不明";
            var outputLength = info.OutputReportLength > 0 ? info.OutputReportLength.ToString() : "不明";
            var featureLength = info.FeatureReportLength > 0 ? info.FeatureReportLength.ToString() : "不明";
            var topLevelCollections = info.TopLevelCollectionCount > 0 ? info.TopLevelCollectionCount.ToString() : "不明";
            var topLevelCollectionPosition = info.TopLevelCollectionIndex;

            static string FormatReportIds(string title, IReadOnlyList<byte> ids)
            {
                return ids.Count > 0
                    ? $"{title}: {string.Join(" ", ids.Select(id => $"{id:X2}"))}"
                    : $"{title}: (なし)";
            }

            var inputIds = FormatReportIds("Input Report IDs", info.InputReportIds);
            var outputIds = FormatReportIds("Output Report IDs", info.OutputReportIds);
            var featureIds = FormatReportIds("Feature Report IDs", info.FeatureReportIds);

            return $"名称: {info.DisplayName}\nVID:0x{info.VendorId:X4} / PID:0x{info.ProductId:X4} / バージョン: 0x{info.VersionNumber:X4} / TLC: {topLevelCollectionPosition} (Nodes: {topLevelCollections})\nInput Report: {inputLength} bytes / Output Report: {outputLength} bytes / Feature Report: {featureLength} bytes\n{inputIds}\n{outputIds}\n{featureIds}";
        }

        private void UpdateLastInputReport(byte[] data)
        {
            var hex = HexConverter.ToHexString(data);
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Application.Current.Dispatcher.Invoke(() =>
            {
                LastInputReport = string.IsNullOrEmpty(hex) ? "(受信データなし)" : hex;
                LastInputReportTimestamp = timestamp;
            });
        }
    }
}
