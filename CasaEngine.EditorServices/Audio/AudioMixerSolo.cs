namespace CasaEngine.EditorServices.Audio;

/// <summary>A bus as the solo computation sees it.</summary>
/// <param name="Name">Bus name (compared ignoring case).</param>
/// <param name="Parent">Parent bus name, null or empty for a root.</param>
/// <param name="IsReturn">True when a send of the asset or of the live mixer targets this bus.</param>
/// <param name="IsReserved">True for a bus the engine keeps for itself (Editor): always audible.</param>
public sealed record AudioMixerSoloNode(string Name, string Parent, bool IsReturn, bool IsReserved);

/// <summary>
/// Pure solo rule of the mixing panel (plan decisions D17 and P53): soloing is a transient mute of every bus that is not
/// part of the soloed signal path.
/// </summary>
public static class AudioMixerSolo
{
    /// <summary>
    /// Fills <paramref name="mutedOut"/> with the names of the nodes to mute. Nothing soloed means nothing muted. Otherwise a
    /// node stays audible when it is a soloed bus, an ancestor or a descendant of one, a return bus or a reserved bus, or an
    /// ancestor of a return or reserved bus; every other node is muted. A parent name that is not a node is simply an
    /// ancestor name (it is never listed in the output).
    /// </summary>
    public static void ComputeMuted(IReadOnlyList<AudioMixerSoloNode> nodes, IReadOnlyCollection<string> soloed, ICollection<string> mutedOut)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(soloed);
        ArgumentNullException.ThrowIfNull(mutedOut);

        if (soloed.Count == 0)
        {
            return;
        }

        var parentByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node != null && !string.IsNullOrEmpty(node.Name) && !parentByName.ContainsKey(node.Name))
            {
                parentByName.Add(node.Name, node.Parent);
            }
        }

        var soloSet = new HashSet<string>(soloed, StringComparer.OrdinalIgnoreCase);
        var audible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string name in soloSet)
        {
            audible.Add(name);
            AddAncestors(name, parentByName, audible);
        }

        for (int index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node == null || string.IsNullOrEmpty(node.Name))
            {
                continue;
            }

            if (node.IsReturn || node.IsReserved)
            {
                audible.Add(node.Name);
                AddAncestors(node.Name, parentByName, audible);
            }

            if (HasSoloedAncestor(node.Name, parentByName, soloSet))
            {
                audible.Add(node.Name);
            }
        }

        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node != null && !string.IsNullOrEmpty(node.Name) && !audible.Contains(node.Name) && listed.Add(node.Name))
            {
                mutedOut.Add(node.Name);
            }
        }
    }

    private static void AddAncestors(string name, Dictionary<string, string> parentByName, HashSet<string> audible)
    {
        string current = name;
        for (int step = 0; step <= parentByName.Count; step++)
        {
            if (!parentByName.TryGetValue(current, out string parent) || string.IsNullOrEmpty(parent))
            {
                return;
            }

            audible.Add(parent);
            current = parent;
        }
    }

    private static bool HasSoloedAncestor(string name, Dictionary<string, string> parentByName, HashSet<string> soloSet)
    {
        string current = name;
        for (int step = 0; step <= parentByName.Count; step++)
        {
            if (!parentByName.TryGetValue(current, out string parent) || string.IsNullOrEmpty(parent))
            {
                return false;
            }

            if (soloSet.Contains(parent))
            {
                return true;
            }

            current = parent;
        }

        return false;
    }
}
