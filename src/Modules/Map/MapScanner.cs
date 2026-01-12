using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ColdStrategyDb.Modules.Map;

public static class MapScanner
{
    public static HashSet<Vector2> DebugNodes { get; private set; }
    public static List<Vector2[]> DebugBorders { get; private set; }
    public static MapData Scan(Image image, float simplificationTolerance = 1f)
    {
        var data = new MapData
        {
            Height = image.GetHeight(),
            Width = image.GetWidth(), 
        };
        
        var nodes = GetSetOfNodes(image);
        GD.Print($"[MapScanner] Found {nodes.Count} Junction Nodes.");
        
        var walker = new TopologyWalker(image, nodes);
        var borders = walker.CollectBorders();
        List<Vector2[]> rawBorders = [];

        for (var i = 0; i < borders.Count; i++)
        {
            var borderData = borders[i]; 
            
            var simplifiedPath = SimplifyBorder([..borderData.Path], simplificationTolerance).ToArray();
            
            borderData.Path = simplifiedPath;
            borders[i] = borderData;
            rawBorders.Add(simplifiedPath);
        }
        
        // 3. CRITICAL FIX: Actually store the data
        data.Borders = borders;
        
        GD.Print($"[MapScanner] Traced {borders.Count} Raw Borders.");

        DebugNodes = nodes;
        DebugBorders = rawBorders;
        
        return data;
    }

    private static List<Vector2> SimplifyBorder(List<Vector2> pointList, float epsilon)
    {
        if (pointList == null || pointList.Count < 3) return pointList;

        float dMax = 0f;
        int index = 0;
        int end = pointList.Count - 1;

        // Find the point with the maximum distance
        for (int i = 1; i < end; i++)
        {
            float d = GetPerpendicularDistance(pointList[i], pointList[0], pointList[end]);

            if (d > dMax)
            {
                index = i;
                dMax = d;
            }
        }

        var resultList = new List<Vector2>();

        // If max distance is greater than epsilon, recursively simplify
        if (dMax > epsilon)
        {
            // Recursive call
            var leftSide = pointList.GetRange(0, index + 1);
            var rightSide = pointList.GetRange(index, pointList.Count - index);

            var recResults1 = SimplifyBorder(leftSide, epsilon);
            var recResults2 = SimplifyBorder(rightSide, epsilon);

            // Merge results without LINQ
            // 1. Add the first segment fully
            resultList.AddRange(recResults1);
            
            // 2. Remove the last point of the first segment because it is 
            //    duplicated as the first point of the second segment.
            resultList.RemoveAt(resultList.Count - 1);
            
            // 3. Add the second segment
            resultList.AddRange(recResults2);
        }
        else
        {
            // Keep only the endpoints
            resultList.Add(pointList[0]);
            resultList.Add(pointList[end]);
        }

        return resultList;
    }

    private static float GetPerpendicularDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        if (lineStart == lineEnd)
        {
            return point.DistanceTo(lineStart);
        }
        
        var numerator = Mathf.Abs((lineEnd.X - lineStart.X) * (lineStart.Y - point.Y) - (lineStart.X - point.X) * (lineEnd.Y - lineStart.Y));
        var denominator = lineStart.DistanceTo(lineEnd);

        return numerator / denominator;
    }

    private static HashSet<Vector2> GetSetOfNodes(Image image)
    {
        var set = new HashSet<Vector2>();
        var detectedColors = new HashSet<Color>();

        for (var y = -1; y < image.GetHeight(); y++)
        {
            for (var x = -1; x < image.GetWidth(); x++)
            {
                var topLeft = GetPixel(image, x, y);
                var topRight = GetPixel(image, x+1, y);
                var bottomLeft = GetPixel(image, x, y+1);
                var bottomRight = GetPixel(image, x+1, y+1);
                
                var uniqueCount = 1;
                if (topRight != topLeft) uniqueCount++;
                if (bottomLeft != topLeft && bottomLeft != topRight) uniqueCount++;
                if (bottomRight != topLeft && bottomRight != topRight && bottomRight != bottomLeft) uniqueCount++;
                
                var isCheckerboard = (bottomLeft == topRight) && 
                                      (bottomRight == topLeft) && 
                                      (topLeft != topRight);

                if (uniqueCount > 2 || isCheckerboard)
                {
                    set.Add(new Vector2(x, y));
                    detectedColors.Add(topLeft);
                    detectedColors.Add(topRight);
                    detectedColors.Add(bottomLeft);
                    detectedColors.Add(bottomRight);
                }
            }
        }

        return set;
    }

    private static Color GetPixel(Image image, int x, int y)
    {
        if (x > image.GetWidth() - 1 || y > image.GetHeight() - 1 || x < 0 || y < 0)
        {
            return Colors.Black;
        }

        return image.GetPixel(x, y);
    }
}