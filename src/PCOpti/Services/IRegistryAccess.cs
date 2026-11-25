using PCOpti.Models;

namespace PCOpti.Services;

/// <summary>
/// Thin abstraction over the Windows registry so tweak logic can be unit-tested with a fake.
/// Paths are relative to the hive, e.g. @"Software\Microsoft\GameBar".
/// </summary>
public interface IRegistryAccess
{
    RegValue? GetValue(RegHive hive, string path, string name);
    void SetValue(RegHive hive, string path, string name, RegValue value);
    /// <summary>Deletes a value. Does nothing if it doesn't exist.</summary>
    void DeleteValue(RegHive hive, string path, string name);
    bool KeyExists(RegHive hive, string path);
    /// <summary>All values in a key (empty if the key doesn't exist).</summary>
    IReadOnlyList<KeyValuePair<string, RegValue>> GetValues(RegHive hive, string path);
}
