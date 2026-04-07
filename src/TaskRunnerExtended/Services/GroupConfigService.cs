namespace TaskRunnerExtended.Services;

using System.Text.Json;
using System.Text.Json.Serialization;

using TaskRunnerExtended.Models;

/// <summary>
/// Reads and writes task group configuration files.
/// Merges shared (task-runner-extended-am.json) and local (task-runner-extended-am.local.json).
/// Local file takes precedence for groups with the same name.
/// </summary>
public class GroupConfigService
{
    private const string SharedFileName = "task-runner-extended-am.json";
    private const string LocalFileName = "task-runner-extended-am.local.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Loads and merges groups from both shared and local config files.
    /// Local groups override shared groups with the same name.
    /// </summary>
    public List<TaskGroup> LoadGroups(string solutionDirectory)
    {
        var sharedGroups = LoadSharedGroups(solutionDirectory);
        var localGroups = LoadLocalGroups(solutionDirectory);

        // Merge: local overrides shared by name
        var merged = new Dictionary<string, TaskGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in sharedGroups)
        {
            merged[group.Name] = group;
        }

        foreach (var group in localGroups)
        {
            merged[group.Name] = group;
        }

        return merged.Values.ToList();
    }

    /// <summary>
    /// Loads groups from the shared config file only.
    /// </summary>
    public List<TaskGroup> LoadSharedGroups(string solutionDirectory)
    {
        return LoadFile(Path.Combine(solutionDirectory, SharedFileName));
    }

    /// <summary>
    /// Loads groups from the local config file only.
    /// </summary>
    public List<TaskGroup> LoadLocalGroups(string solutionDirectory)
    {
        return LoadFile(Path.Combine(solutionDirectory, LocalFileName));
    }

    /// <summary>
    /// Saves a group to the specified location (local or shared).
    /// </summary>
    public void SaveGroup(string solutionDirectory, TaskGroup group, bool toShared = false)
    {
        var fileName = toShared ? SharedFileName : LocalFileName;
        var filePath = Path.Combine(solutionDirectory, fileName);

        var config = LoadConfig(filePath);

        // Replace existing group with same name, or add new
        var existingIndex = config.Groups.FindIndex(g =>
            g.Name.Equals(group.Name, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            config.Groups[existingIndex] = group;
        }
        else
        {
            config.Groups.Add(group);
        }

        SaveConfig(filePath, config);
    }

    /// <summary>
    /// Deletes a group from the specified config file.
    /// </summary>
    public void DeleteGroup(string solutionDirectory, string groupName, bool fromShared = false)
    {
        var fileName = fromShared ? SharedFileName : LocalFileName;
        DeleteGroupFromFile(Path.Combine(solutionDirectory, fileName), groupName);
    }

    /// <summary>
    /// Adds a task to an existing group, or creates the group if it doesn't exist.
    /// </summary>
    public void AddTaskToGroup(string solutionDirectory, string groupName, TaskGroupEntry entry, bool toShared = false)
    {
        var fileName = toShared ? SharedFileName : LocalFileName;
        var filePath = Path.Combine(solutionDirectory, fileName);
        var config = LoadConfig(filePath);

        var group = config.Groups.FirstOrDefault(g =>
            g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));

        if (group is null)
        {
            group = new TaskGroup { Name = groupName };
            config.Groups.Add(group);
        }

        // Don't add duplicate
        if (!group.Tasks.Any(t => t.Source == entry.Source && t.Task == entry.Task))
        {
            entry.Order = group.Tasks.Count + 1;
            group.Tasks.Add(entry);
        }

        SaveConfig(filePath, config);
    }

    /// <summary>
    /// Removes a task from a group in the specified config file.
    /// </summary>
    public void RemoveTaskFromGroup(string solutionDirectory, string groupName, string source, string taskLabel, bool fromShared = false)
    {
        var fileName = fromShared ? SharedFileName : LocalFileName;
        var filePath = Path.Combine(solutionDirectory, fileName);
        var config = LoadConfig(filePath);

        var group = config.Groups.FirstOrDefault(g =>
            g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(source))
            group?.Tasks.RemoveAll(t => t.Task == taskLabel);
        else
            group?.Tasks.RemoveAll(t => t.Source == source && t.Task == taskLabel);

        SaveConfig(filePath, config);
    }

    /// <summary>
    /// Returns all group names for the "Add to Group..." menu.
    /// </summary>
    public List<string> GetGroupNames(string solutionDirectory)
    {
        return LoadGroups(solutionDirectory).Select(g => g.Name).ToList();
    }

    private static List<TaskGroup> LoadFile(string filePath)
    {
        return LoadConfig(filePath).Groups;
    }

    private static TaskGroupConfig LoadConfig(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return new TaskGroupConfig();

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<TaskGroupConfig>(json, JsonOptions) ?? new TaskGroupConfig();
        }
        catch
        {
            return new TaskGroupConfig();
        }
    }

    private static void SaveConfig(string filePath, TaskGroupConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);

            // Atomic write: write to temp file, then move
            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (IOException)
        {
            // Concurrent access — retry once after short delay
            try
            {
                Thread.Sleep(100);
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Give up silently
            }
        }
    }

    private static void DeleteGroupFromFile(string filePath, string groupName)
    {
        var config = LoadConfig(filePath);
        config.Groups.RemoveAll(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        if (config.Groups.Count > 0 || File.Exists(filePath))
        {
            SaveConfig(filePath, config);
        }
    }
}
