using System.Text;

namespace CardDispenserAgent.Protocol;

public static class CommandParser
{
    public static string Parse(byte[] packet)
    {
        if (packet.Length < 5)
            return string.Empty;

        if (packet[0] != Constants.STX)
            return string.Empty;

        int length = (packet[1] << 8) | packet[2];

        return Encoding.ASCII.GetString(packet, 3, length);
    }

    public static bool IsAck(byte[] data)
    {
        return data.Length == 1 &&
               data[0] == Constants.ACK;
    }

    public static string ToHex(byte[] data)
    {
        return BitConverter.ToString(data).Replace("-", " ");
    }
}