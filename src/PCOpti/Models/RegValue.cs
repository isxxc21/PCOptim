using System.Globalization;

namespace PCOpti.Models;

public enum RegHive { CurrentUser, LocalMachine }

public enum RegKind { String, ExpandString, DWord, QWord, Binary, MultiString }

/// <summary>
/// A registry value in a canonical, serialisable form. Data is always a string so a value can be
/// stored in the JSON backup and compared for equality:
/// DWord/QWord = decimal, Binary = hex ("9012038010000000"), MultiString = lines joined with '\n'.
/// </summary>
public sealed record RegValue(RegKind Kind, string Data)
{
    public static RegValue DWord(int value) => new(RegKind.DWord, value.ToString(CultureInfo.InvariantCulture));
    public static RegValue Str(string value) => new(RegKind.String, value);
    public static RegValue Binary(params byte[] bytes) => new(RegKind.Binary, Convert.ToHexString(bytes));

    public int? AsInt() =>
        Kind == RegKind.DWord && int.TryParse(Data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    public byte[] AsBytes() => Kind == RegKind.Binary ? Convert.FromHexString(Data) : Array.Empty<byte>();

    public override string ToString() => Kind switch
    {
        RegKind.Binary => string.Join(" ", Enumerable.Range(0, Data.Length / 2).Select(i => Data.Substring(i * 2, 2))),
        RegKind.String or RegKind.ExpandString => $"\"{Data}\"",
        _ => Data,
    };

    public static string HiveName(RegHive hive) => hive == RegHive.CurrentUser ? "HKCU" : "HKLM";
}
