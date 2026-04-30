using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;


public static class XYZParser
{
    // Inches to meters (0.0254)
    private const float InchesToMeters = 0.0254f;

    public static List<Vector3> ParseContent(string content)
    {
        List<Vector3> positions = new List<Vector3>();
        if (string.IsNullOrEmpty(content)) return positions;

        string[] lines = content.Split(new[] { "\r\n", "\n", "\r" }, System.StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] tokens = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3)
            {
                Debug.LogWarning($"XYZParser: skipping malformed line {i + 1}: '{line}'");
                continue;
            }

            if (float.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                positions.Add(new Vector3(x, y, z) * InchesToMeters);
            }
            else
            {
                Debug.LogWarning($"XYZParser: failed to parse line {i + 1}: '{line}'");
            }
        }

        return positions;
    }

    public static List<Vector3> ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"XYZParser: file not found: {filePath}");
            return new List<Vector3>();
        }
        string content = File.ReadAllText(filePath);
        return ParseContent(content);
    }
}