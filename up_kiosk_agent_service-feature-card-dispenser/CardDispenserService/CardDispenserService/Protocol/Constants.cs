namespace CardDispenserAgent.Protocol;

public static class Constants
{
    public const byte STX = 0xF2;
    public const byte ACK = 0x06;

    public const string Initialize = "C00";

    public const string Dispense = "C221";

    public const string Withdraw = "C6C";

    public const string Capture = "C31";

    //public const string ReadStatus = "C30";

    //public const string ReadShutterStatus = "C34";

    //public const string ReadHopperStatus = "C35";

    //public const string Reset = "C01";
}