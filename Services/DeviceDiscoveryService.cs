using System.Collections.Generic;
using System.Threading.Tasks;
using CustomHidChecker.Models;

namespace CustomHidChecker.Services
{
    public sealed class DeviceDiscoveryService
    {
        public Task<IReadOnlyList<HidDeviceInfo>> EnumerateAsync()
        {
            return Task.Run<IReadOnlyList<HidDeviceInfo>>(HidEnumerator.Enumerate);
        }
    }
}
