/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using System.Collections.Concurrent;

namespace SyncMPSC.Ipc.Sockets;

public interface IPropertyService
{
    static abstract string? GetProperty(string key);
    static abstract string? GetPropertyOrDefault(string key, string defaultValue);
    static abstract int GetPropertyAsIntOrDefault(string key, int defaultValue);
    static abstract bool GetPropertyAsBoolean(string key);
    static abstract List<Dictionary<string, string>> GetPropertyTable(string prefix);
    static abstract void LoadFromFile(string filePath);
}


public class PropertyService : IPropertyService
{
    private static readonly ConcurrentDictionary<string, string> _properties = new();
    private static readonly char[] anyOf = ['=', ':'];

    public static void SetProperty(string key, string value) => _properties[key] = value;

    public static string? GetProperty(string key) =>
        _properties.TryGetValue(key, out var value) ? value : null;

    public static string? GetPropertyOrDefault(string key, string defaultValue) =>
        _properties.TryGetValue(key, out var value) ? value : defaultValue;

    public static int GetPropertyAsIntOrDefault(string key, int defaultValue)
    {
        var val = GetProperty(key);
        return int.TryParse(val, out int result) ? result : defaultValue;
    }

    public static bool GetPropertyAsBoolean(string key)
    {
        var val = GetProperty(key);
        return val != null && (val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1");
    }

    /// <summary>
    /// Loads Properties in the format key=value from a text file.
    /// Comments starting with '#' and empty lines are ignored.
    /// </summary>
    public static void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("configuration file not found", filePath);

        foreach (var line in File.ReadLines(filePath))
        {
            var trimmedLine = line.Trim();

            // skip comments
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
                continue;

            // search for key-value separator
            int separatorIndex = trimmedLine.IndexOfAny(anyOf);
            if (separatorIndex > 0)
            {
                string key = trimmedLine.Substring(0, separatorIndex).Trim();
                string value = trimmedLine.Substring(separatorIndex + 1).Trim();
                _properties[key] = value;
            }
        }
    }

    /// <summary>
    /// Simulates a table of properties based on a common prefix and indexed keys.
    /// Example: messaging.queue.0.id=ABC, messaging.queue.0.port=5555
    /// </summary>
    public static List<Dictionary<string, string>> GetPropertyTable(string prefix)
    {
        var results = new Dictionary<string, Dictionary<string, string>>();
        var prefixPartsCount = prefix.Split('.', StringSplitOptions.RemoveEmptyEntries).Length;

        foreach (var kvp in _properties.Where(p => p.Key.StartsWith(prefix)))
        {
            // Expected format: prefix.index.suffix (e.g., messaging.queue.0.id)
            var parts = kvp.Key.Split('.');
            if (parts.Length > prefixPartsCount)
            {
                // The index is the part right after the prefix
                string index = parts[prefixPartsCount];
                string suffix = parts.Last();

                if (!results.ContainsKey(index))
                    results[index] = new Dictionary<string, string>();

                results[index][suffix] = kvp.Value;
            }
        }
        return results.Values.ToList();
    }
}
