using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace Eclipse.Services;
internal static class DebugService
{
    const int MAX_CHILDREN = 20;
    const int MAX_HITS = 20;

    static readonly string[] TabLabels =
    [
        "Equipment",
        "Crafting",
        "Blood Pool",
        "Attributes"
    ];

    /// <summary>
    /// Dumps Character UI hierarchy data to logs to locate tab containers.
    /// </summary>
    public static void DumpCharacterUi()
    {
        try
        {
            Core.Log.LogInfo("[Debug UI] F1 pressed. Scanning Character UI labels...");

            List<TextMeshProUGUI> hits = FindLabelHits();
            if (hits.Count == 0)
            {
                Core.Log.LogWarning("[Debug UI] No Character tab labels found. Open the Character window and press F1 again.");
                return;
            }

            Core.Log.LogInfo($"[Debug UI] Found {hits.Count} label(s).");

            int limit = Math.Min(hits.Count, MAX_HITS);
            for (int i = 0; i < limit; i++)
            {
                LogLabelHit(hits[i]);
            }

            if (hits.Count > MAX_HITS)
            {
                Core.Log.LogInfo($"[Debug UI] Output truncated after {MAX_HITS} hits.");
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[Debug UI] Failed to dump Character UI: {ex}");
        }
    }

    /// <summary>
    /// Finds TextMeshProUGUI nodes that match known Character tab labels.
    /// </summary>
    /// <returns>A list of matching TMP labels.</returns>
    static List<TextMeshProUGUI> FindLabelHits()
    {
        List<TextMeshProUGUI> hits = [];

        foreach (TextMeshProUGUI text in UnityEngine.Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (text == null)
            {
                continue;
            }

            string value = text.text?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (MatchesTabLabel(value))
            {
                hits.Add(text);
            }
        }

        return hits;
    }

    /// <summary>
    /// Checks whether a label matches known Character tab text.
    /// </summary>
    /// <param name="text">The label text to check.</param>
    /// <returns>True if the label matches a known tab.</returns>
    static bool MatchesTabLabel(string text)
    {
        for (int i = 0; i < TabLabels.Length; i++)
        {
            if (text.Contains(TabLabels[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Logs the hierarchy around a label hit to help identify tab containers.
    /// </summary>
    /// <param name="text">The TMP label component.</param>
    static void LogLabelHit(TextMeshProUGUI text)
    {
        Transform transform = text.transform;
        Core.Log.LogInfo($"[Debug UI] Label '{text.text}' at {GetPath(transform)} (active={transform.gameObject.activeInHierarchy})");

        Transform parent = transform.parent;
        if (parent != null)
        {
            Core.Log.LogInfo($"[Debug UI] Parent: {GetPath(parent)} (children={parent.childCount})");
            LogChildren(parent);
        }

        Transform grandParent = parent?.parent;
        if (grandParent != null)
        {
            Core.Log.LogInfo($"[Debug UI] Grandparent: {GetPath(grandParent)} (children={grandParent.childCount})");
            LogChildren(grandParent);
        }
    }

    /// <summary>
    /// Logs the immediate children of a transform for context.
    /// </summary>
    /// <param name="parent">The parent transform.</param>
    static void LogChildren(Transform parent)
    {
        int limit = Math.Min(parent.childCount, MAX_CHILDREN);
        for (int i = 0; i < limit; i++)
        {
            Transform child = parent.GetChild(i);
            Core.Log.LogInfo($"[Debug UI]   child[{i}]: {child.name} (active={child.gameObject.activeSelf})");
        }

        if (parent.childCount > MAX_CHILDREN)
        {
            Core.Log.LogInfo($"[Debug UI]   ... {parent.childCount - MAX_CHILDREN} more child(ren) not shown.");
        }
    }

    /// <summary>
    /// Builds a full path for a transform in the scene hierarchy.
    /// </summary>
    /// <param name="transform">The transform to describe.</param>
    /// <returns>The path from the root to the transform.</returns>
    static string GetPath(Transform transform)
    {
        var sb = new StringBuilder();
        Transform current = transform;

        while (current != null)
        {
            if (sb.Length == 0)
            {
                sb.Insert(0, current.name);
            }
            else
            {
                sb.Insert(0, $"{current.name}/");
            }

            current = current.parent;
        }

        return sb.ToString();
    }
}
