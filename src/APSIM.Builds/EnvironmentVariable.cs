using System;

namespace APSIM.Builds;

/// <summary>
/// Code to read an environment variable or throw if not found.
/// </summary>
public static class EnvironmentVariable
{
    /// <summary>
    /// Get the value of the environment variable, or throw if not set.
    /// </summary>
    public static string Read(string variableName, string description)
    {
        if (string.IsNullOrEmpty(variableName))
            throw new ArgumentNullException(nameof(variableName));

        string value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException($"Unable to read {description} from environment: variable {variableName} not set");

        return value;
    }
}
