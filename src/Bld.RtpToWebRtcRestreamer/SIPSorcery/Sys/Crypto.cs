namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;

public static class Crypto
{
    private const int DEFAULT_RANDOM_LENGTH = 10;    // Number of digits to return for default random numbers.

    private const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string GetRandomString(int length)
    {
        var buffer = new char[length];

        for (var i = 0; i < length; i++)
        {
            buffer[i] = CHARS[Random.Shared.Next(CHARS.Length)];
        }
        return new string(buffer);
    }

    /// <summary>
    /// Returns a random number of a specified length.
    /// </summary>
    public static int GetRandomInt(int length)
    {
        var randomStart = 1000000000;
        var randomEnd = int.MaxValue;

        if (length > 0 && length < DEFAULT_RANDOM_LENGTH)
        {
            randomStart = Convert.ToInt32(Math.Pow(10, length - 1));
            randomEnd = Convert.ToInt32(Math.Pow(10, length) - 1);
        }

        return Random.Shared.Next(randomStart, randomEnd);
    }

    public static ushort GetRandomUInt16()
    {
        var uint16Buffer = new byte[2];
        Random.Shared.NextBytes(uint16Buffer);
        return BitConverter.ToUInt16(uint16Buffer, 0);
    }

    public static uint GetRandomUInt(bool noZero = false)
    {
        var uint32Buffer = new byte[4];
        Random.Shared.NextBytes(uint32Buffer);
        var randomUint = BitConverter.ToUInt32(uint32Buffer, 0);

        if (noZero && randomUint == 0)
        {
            Random.Shared.NextBytes(uint32Buffer);
            randomUint = BitConverter.ToUInt32(uint32Buffer, 0);
        }

        return randomUint;
    }

    public static ulong GetRandomULong()
    {
        var uint64Buffer = new byte[8];
        Random.Shared.NextBytes(uint64Buffer);
        return BitConverter.ToUInt64(uint64Buffer, 0);
    }
}