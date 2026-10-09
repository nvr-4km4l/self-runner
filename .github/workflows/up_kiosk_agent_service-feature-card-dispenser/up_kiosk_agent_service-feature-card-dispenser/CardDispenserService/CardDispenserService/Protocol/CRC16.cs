using System;

namespace CardDispenserAgent.Protocol;

public static class CRC16
{
    public static byte[] Calculate(byte[] data)
    {
        ushort crc = 0x0000;
        const ushort poly = 0x1021;

        foreach (byte b in data)
        {
            for (int i = 0; i < 8; i++)
            {
                bool bit = ((b >> (7 - i)) & 1) == 1;
                bool c15 = ((crc >> 15) & 1) == 1;

                crc <<= 1;

                if (c15 ^ bit)
                    crc ^= poly;
            }
        }

        crc &= 0xFFFF;

        return
        [
            (byte)((crc >> 8) & 0xFF),
            (byte)(crc & 0xFF)
        ];
    }
}