using System.Text;

namespace CardDispenserAgent.Protocol;

public static class ResponseTranslator
{
    // ---- cm (command code) lookup: first char after P/N ----
    private static readonly Dictionary<char, string> CommandNames = new()
    {
        ['0'] = "Initialize",
        ['1'] = "Status Request",
        ['2'] = "Entry",
        ['3'] = "Card Carry",
        ['l'] = "Intake/Withdraw", // 0x6C, lowercase L
    };

    // error code table (9.3, m/s:37) ----
    private static readonly Dictionary<string, string> ErrorCodes = new()
    {
        ["00"] = "Command code is unidentified (typo or wrong case)",
        ["01"] = "Parameter is not correct",
        ["02"] = "Command execution is impossible",
        ["03"] = "Hardware is not present",
        ["04"] = "Command data error",
        ["05"] = "Tried to feed card before IC contact release command",
        ["06"] = "ICRW does not have keys to decipher the data",
        ["10"] = "Card jam",
        ["11"] = "Shutter failure",
        ["12"] = "Sensor failure of PD1/PD2/PD3/PDI, or card remains inside",
        ["13"] = "Irregular card length (too long)",
        ["14"] = "Irregular card length (too short)",
        ["15"] = "F-ROM error",
        ["16"] = "The card was moved forcibly",
        ["17"] = "Jam error at retrieve",
        ["18"] = "SW1 or SW2 error",
        ["20"] = "Read error (parity error)",
        ["21"] = "Read error",
        ["22"] = "Write error",
        ["23"] = "Read error (SS-ES-LRC only)",
        ["24"] = "Read error (no encode / no magnetic stripe)",
        ["25"] = "Write verify error (quality error)",
        ["26"] = "Read error (no SS)",
        ["27"] = "Read error (no ES)",
        ["28"] = "Read error (LRC error)",
        ["29"] = "Write verify error (data discordance)",
        ["30"] = "Power down",
        ["31"] = "DSR signal turned OFF",
        ["40"] = "Card was pulled out during capture",
        ["41"] = "Failure at IC contact solenoid or sensor ICD",
        ["43"] = "Card could not be set to IC contact position",
        ["45"] = "ICRW ejected the card forcibly",
        ["46"] = "Ejected card was not withdrawn within the timeout",
        ["50"] = "Retract counter overflow",
        ["51"] = "Motor error",
        ["60"] = "Abnormal power-line (Vcc) condition on IC card",
        ["61"] = "Receiving error of ATR",
        ["62"] = "Specified protocol does not match the IC card's",
        ["63"] = "IC card communication error (no response)",
        ["64"] = "IC card communication error (other)",
        ["65"] = "Command sent before receiving ATR",
        ["66"] = "IC card not supported by this ICRW",
        ["69"] = "IC card not supported under Protocol EMV",
        ["A0"] = "No card at the hopper (hopper empty)",
        ["A5"] = "Card jam at the hopper",
        ["A6"] = "Hopping kicker could not return to home position",
        ["AB"] = "Reject-stacker missing or full",
        ["B0"] = "Initialize command not received yet",
        ["SA"] = "SCT is in Supervisor program code area (needs Initialize)",
    };

    // ---- st1st0 (ICRW status) table (§9.2) ----
    private static readonly Dictionary<string, string> IcrwStatus = new()
    {
        ["00"] = "No card detected within ICRW",
        ["01"] = "Card is at the Gate",
        ["02"] = "Card is inside ICRW (transport)",
    };

    public static string Translate(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "Empty or unparseable frame.";

        char kind = raw[0];

        return kind switch
        {
            'P' => TranslatePositive(raw),
            'N' => TranslateNegative(raw),
            _ => $"Unrecognized response marker '{kind}'. Raw: {raw}",
        };
    }

    private static string TranslatePositive(string raw)
    {
        if (raw.Length < 3)
            return $"Positive response, but too short to decode fully. Raw: {raw}";

        char cmChar = raw[1];
        char pmChar = raw[2];
        string cmName = CommandNames.TryGetValue(cmChar, out var name) ? name : $"Unknown command (cm='{cmChar}')";

        var sb = new StringBuilder();
        sb.Append($"Success — {cmName} (pm='{pmChar}') completed.");

        // st1st0st2 + Se only present on Status Request / Intake-Withdraw responses
        if (raw.Length >= 6)
        {
            string st1st0 = raw.Substring(3, 2);
            char st2 = raw[5];

            if (IcrwStatus.TryGetValue(st1st0, out var icrwMeaning))
                sb.Append($" ICRW: {icrwMeaning}.");

            sb.Append($" {DescribeHopperByte(st2)}");

            if (raw.Length >= 7)
            {
                char se = raw[6];
                sb.Append($" {DescribeSensorByte(se)}");
            }
        }

        return sb.ToString();
    }

    private static string TranslateNegative(string raw)
    {
        if (raw.Length < 5)
            return $"Negative response, but too short to decode fully. Raw: {raw}";

        char cmChar = raw[1];
        char pmChar = raw[2];
        string errorCode = raw.Substring(3, 2);
        string cmName = CommandNames.TryGetValue(cmChar, out var name) ? name : $"Unknown command (cm='{cmChar}')";
        string errorMeaning = ErrorCodes.TryGetValue(errorCode, out var meaning)
            ? meaning
            : $"Unknown error code '{errorCode}'";

        return $"ERROR — {cmName} (pm='{pmChar}') failed: [{errorCode}] {errorMeaning}.";
    }

    private static string DescribeHopperByte(char st2)
    {
        int b = (byte)st2;
        bool stacker = (b & 0b0000_1000) != 0;
        bool hopper1 = (b & 0b0001_0000) != 0;
        bool nearEnd = (b & 0b0010_0000) != 0;

        var parts = new List<string>();
        parts.Add(hopper1 ? "Hopper 1 has cards" : "Hopper 1 is empty");
        if (nearEnd) parts.Add("running low (near-end)");
        if (stacker) parts.Add("card present in reject-stacker");

        return $"Hopper: {string.Join(", ", parts)}.";
    }

  
    private static string DescribeSensorByte(char se)
    {
        int b = (byte)se;
        bool sw1 = (b & 0b0001_0000) != 0;
        bool sw2 = (b & 0b0000_1000) != 0;
        bool pd1 = (b & 0b0000_0100) != 0;
        bool pd2 = (b & 0b0000_0010) != 0;
        bool pd3 = (b & 0b0000_0001) != 0;

        var parts = new List<string>();
        parts.Add(sw1 ? "card at SW1" : "no card at SW1");
        parts.Add(sw2 ? "shutter open" : "shutter closed");
        if (pd1) parts.Add("card at PD1");
        if (pd2) parts.Add("card at PD2");
        if (pd3) parts.Add("card at PD3");

        return $"Sensors: {string.Join(", ", parts)}.";
    }
}