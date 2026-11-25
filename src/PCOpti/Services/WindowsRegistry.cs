using System.Globalization;
using Microsoft.Win32;
using PCOpti.Models;

namespace PCOpti.Services;

/// <summary>Real registry access. Always uses the 64-bit view so HKLM paths aren't redirected.</summary>
public sealed class WindowsRegistry : IRegistryAccess
{
    private static RegistryKey Base(RegHive hive) => RegistryKey.OpenBaseKey(
        hive == RegHive.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine,
        RegistryView.Registry64);

    public RegValue? GetValue(RegHive hive, string path, string name)
    {
        using var root = Base(hive);
        using var key = root.OpenSubKey(path, writable: false);
        if (key is null) return null;
        var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (raw is null) return null;
        return Convert(key.GetValueKind(name), raw);
    }

    public void SetValue(RegHive hive, string path, string name, RegValue value)
    {
        using var root = Base(hive);
        using var key = root.CreateSubKey(path, writable: true)
                        ?? throw new UnauthorizedAccessException($"Could not open {RegValue.HiveName(hive)}\\{path} for writing.");
        switch (value.Kind)
        {
            case RegKind.DWord:
                key.SetValue(name, int.Parse(value.Data, CultureInfo.InvariantCulture), RegistryValueKind.DWord); break;
            case RegKind.QWord:
                key.SetValue(name, long.Parse(value.Data, CultureInfo.InvariantCulture), RegistryValueKind.QWord); break;
            case RegKind.Binary:
                key.SetValue(name, value.AsBytes(), RegistryValueKind.Binary); break;
            case RegKind.ExpandString:
                key.SetValue(name, value.Data, RegistryValueKind.ExpandString); break;
            case RegKind.MultiString:
                key.SetValue(name, value.Data.Split('\n'), RegistryValueKind.MultiString); break;
            default:
                key.SetValue(name, value.Data, RegistryValueKind.String); break;
        }
    }

    public void DeleteValue(RegHive hive, string path, string name)
    {
        using var root = Base(hive);
        using var key = root.OpenSubKey(path, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public bool KeyExists(RegHive hive, string path)
    {
        using var root = Base(hive);
        using var key = root.OpenSubKey(path, writable: false);
        return key is not null;
    }

    public IReadOnlyList<KeyValuePair<string, RegValue>> GetValues(RegHive hive, string path)
    {
        using var root = Base(hive);
        using var key = root.OpenSubKey(path, writable: false);
        if (key is null) return Array.Empty<KeyValuePair<string, RegValue>>();
        var list = new List<KeyValuePair<string, RegValue>>();
        foreach (var name in key.GetValueNames())
        {
            var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (raw is null) continue;
            var v = Convert(key.GetValueKind(name), raw);
            if (v is not null) list.Add(new(name, v));
        }
        return list;
    }

    private static RegValue? Convert(RegistryValueKind kind, object raw) => kind switch
    {
        RegistryValueKind.DWord => new RegValue(RegKind.DWord, System.Convert.ToInt32(raw).ToString(CultureInfo.InvariantCulture)),
        RegistryValueKind.QWord => new RegValue(RegKind.QWord, System.Convert.ToInt64(raw).ToString(CultureInfo.InvariantCulture)),
        RegistryValueKind.Binary => new RegValue(RegKind.Binary, System.Convert.ToHexString((byte[])raw)),
        RegistryValueKind.ExpandString => new RegValue(RegKind.ExpandString, (string)raw),
        RegistryValueKind.MultiString => new RegValue(RegKind.MultiString, string.Join('\n', (string[])raw)),
        RegistryValueKind.String => new RegValue(RegKind.String, (string)raw),
        _ => null,
    };
}
