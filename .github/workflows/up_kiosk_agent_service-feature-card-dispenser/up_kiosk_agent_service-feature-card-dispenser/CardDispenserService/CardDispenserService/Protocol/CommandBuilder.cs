using System.Text;

namespace CardDispenserAgent.Protocol;

public static class CommandBuilder
{
    public static byte[] Build(string command, string payload = "")
    {
        List<byte> packet = new();

        string text = command + payload;

        byte[] body = Encoding.ASCII.GetBytes(text);

        ushort length = (ushort)body.Length;

        byte lengthHigh = (byte)(length >> 8);
        byte lengthLow = (byte)(length & 0xFF);

        packet.Add(Constants.STX);
        packet.Add(lengthHigh);
        packet.Add(lengthLow);

        packet.AddRange(body);

        List<byte> crcInput = new()
        {
            Constants.STX,
            lengthHigh,
            lengthLow
        };

        crcInput.AddRange(body);

        byte[] crc = CRC16.Calculate(crcInput.ToArray());

        packet.AddRange(crc);

        return packet.ToArray();
    }

    public static string ToHex(byte[] data)
    {
        return BitConverter.ToString(data).Replace("-", " ");
    }
}