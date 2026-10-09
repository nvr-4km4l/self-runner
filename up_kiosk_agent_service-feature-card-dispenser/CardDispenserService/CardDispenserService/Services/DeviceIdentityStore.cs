using System.Text.Json;

namespace CardDispenserAgent.Services
{
    public class DeviceIdentityStore
    {
        private readonly string _filePath;

        public DeviceIdentityStore()
        {
            var folderPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "CardDispenserAgent");

            Directory.CreateDirectory(folderPath);

            _filePath = Path.Combine(
                folderPath,
                "device-identity.json");
        }

        public string GetOrCreateDeviceId()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);

                    var identity = JsonSerializer.Deserialize<DeviceIdentityStorage>(json);

                    if (!string.IsNullOrEmpty(identity?.DeviceId))
                    {
                        return identity.DeviceId;
                    }
                }
                catch
                {
                    // Generate a new DeviceId if the file cannot be read.
                }
            }

            var deviceId = Guid.NewGuid().ToString();

            var newIdentity = new DeviceIdentityStorage
            {
                DeviceId = deviceId
            };

            var newJson = JsonSerializer.Serialize(
                newIdentity,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(_filePath, newJson);

            return deviceId;
        }

        private class DeviceIdentityStorage
        {
            public string DeviceId { get; set; } = string.Empty;
        }

    }
}

