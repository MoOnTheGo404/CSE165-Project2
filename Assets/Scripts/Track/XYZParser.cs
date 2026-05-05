using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class XYZParser
{
    // Inches to meters — matches the model's import Scale Factor (0.0254).
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


    public static IEnumerator ParseFileAsync(string path, System.Action<List<Vector3>> onComplete)
    {
        // On Android, StreamingAssets paths are inside the APK and start with "jar:file://".
        // On Editor/Standalone, they're regular file paths and need a "file://" prefix for UnityWebRequest.
        string url = path;
        if (!path.Contains("://"))
        {
            url = "file://" + path;
        }

        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"XYZParser: could not load file from {url}: {www.error}");
            onComplete?.Invoke(new List<Vector3>());
            yield break;
        }

        string content = www.downloadHandler.text;
        onComplete?.Invoke(ParseContent(content));
    }
}