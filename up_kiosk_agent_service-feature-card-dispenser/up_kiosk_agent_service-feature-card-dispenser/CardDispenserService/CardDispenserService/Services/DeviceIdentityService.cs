using System.Net.NetworkInformation;
using System.Net.Sockets;
using CardDispenserAgent.Models;

namespace CardDispenserAgent.Services
{
    public class DeviceIdentityService
    {
        private readonly DeviceIdentityStore _deviceIdentityStore;

        public DeviceIdentityService(DeviceIdentityStore deviceIdentityStore)
        {
            _deviceIdentityStore = deviceIdentityStore;
        }
        public DeviceIdentity GetIdentity()
        {
            var networkInterface = GetActiveNetworkInterface();

            return new DeviceIdentity
            {
                DeviceId = _deviceIdentityStore.GetOrCreateDeviceId(),
                DeviceName = Environment.MachineName,
                MacAddress = GetMacAddress(networkInterface),
                IpAddress = GetIpAddress(networkInterface)
            };
        }

        public bool IsIpAddressMatch(string requestedIpAddress)
        {
            var identity = GetIdentity();

            return string.Equals(
                identity.IpAddress,
                requestedIpAddress,
                StringComparison.OrdinalIgnoreCase);
        }

        private static NetworkInterface? GetActiveNetworkInterface()
        {
            return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(networkInterface =>
                    networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    networkInterface.GetIPProperties().UnicastAddresses.Any(address => address.Address.AddressFamily == AddressFamily.InterNetwork));
        }

        private static string GetMacAddress(NetworkInterface? networkInterface)
        {
            if (networkInterface == null)
                return string.Empty;

            var macAddress = networkInterface
                .GetPhysicalAddress()
                .ToString();

            if (string.IsNullOrEmpty(macAddress))
                return string.Empty;

            return string.Join(
                ":",
                Enumerable
                    .Range(0, macAddress.Length / 2)
                    .Select(i => macAddress.Substring(i * 2, 2)));
        }

        private static string GetIpAddress(NetworkInterface? networkInterface)
        {
            if (networkInterface == null)
                return string.Empty;

            return networkInterface
                .GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork) ?.Address
                .ToString() ?? string.Empty;
        }
    }
}