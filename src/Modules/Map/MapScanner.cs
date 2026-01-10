using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ColdStrategyDb.Modules.Map;

public static class MapScanner
{
    public static MapData Scan(Image image, float simplificationTolerance = 1.0f)
    {
        var data = new MapData
        {
            Width = image.GetWidth(),
            Height = image.GetHeight()
        };

        var visitedEdges = new HashSet<long>();
        var width = image.GetWidth();
        var height = image.GetHeight();
        
        for (var y = 0; y < height; y++ )
        {
            for (var x = 0; x < width; x++ )
            {
                var currentColor = image.GetPixel(x, y);
                if (x + 1 < width)
                {
                    if (currentColor != image.GetPixel(x + 1, y))
                    {
                        var startPos = new Vector2(x + 1, y);
                        var startDir = new Vector2(0, 1);
                        long hash = Walker.HashEdge(startPos, startDir);

                        if (visitedEdges.Contains(hash)) continue;

                        var walker = new Walker(image, visitedEdges);
                        var rawPoints = walker.TraceBorder(startPos, startDir);

                        if (rawPoints.Count <= 1) continue;
                        var cleanPoints = SimplifyPath(rawPoints.ToArray(), simplificationTolerance);
                        data.Borders.Add(new BorderData { Points = cleanPoints });
                    }
                }

                if (y + 1 < height)
                {
                    if (currentColor == image.GetPixel(x, y + 1)) continue;
                    var startPos = new Vector2(x, y + 1);
                    var startDir = new Vector2(1, 0); // Right
                    long hash = Walker.HashEdge(startPos, startDir);

                    if (visitedEdges.Contains(hash)) continue;
                    var walker = new Walker(image, visitedEdges);
                    var rawPoints = walker.TraceBorder(startPos, startDir);
                    
                    if (rawPoints.Count <= 2) continue;
                    var uniquePoints = RemoveConsecutiveDuplicates(rawPoints);

                    // For closed loops: if first ≈ last, optionally remove last to avoid duplicate ends
                    if (uniquePoints.Count >= 3 && uniquePoints[0] == uniquePoints[uniquePoints.Count - 1])
                    {
                        uniquePoints.RemoveAt(uniquePoints.Count - 1);
                    }
                    
                    var cleanPoints = SimplifyPath(rawPoints.ToArray(), simplificationTolerance);
                    data.Borders.Add(new BorderData { Points = cleanPoints });
                }
            }
        }
        
        GD.Print($"[MapScanner] Scan complete. Found {data.Borders.Count} borders.");
        return data;
    }

    private class Walker(Image image, HashSet<long> sharedVisitedSet)
    {
        private readonly Image _image = image;
        private readonly HashSet<long> _visitedEdges = sharedVisitedSet;
        
        public List<Vector2> TraceBorder(Vector2 startPos, Vector2 startDir)
        {
            var points = new List<Vector2>();
            
            var currentPos = startPos;
            var currentDir = startDir;
            var initialPos = startPos;
            var initialDir = startDir;

            // Cache who owns the left/right sides so we detect Nodes
            Color expectedLeft = GetPixelRelative(currentPos, currentDir, true);
            Color expectedRight = GetPixelRelative(currentPos, currentDir, false);

            var safety = 0;
            while (safety < 20000)
            {
                safety++;
                points.Add(currentPos);

                // Mark current edge as visited
                long hash = HashEdge(currentPos, currentDir);
                if (!_visitedEdges.Add(hash)) break; // Merge into existing line

                // Look Ahead
                var nextPos = currentPos + currentDir;
                Color nextLeft = GetPixelRelative(nextPos, currentDir, true);
                Color nextRight = GetPixelRelative(nextPos, currentDir, false);

                var turned = false;

                // Decision Logic (Hand-on-Wall)
                if (nextLeft != expectedLeft)
                {
                    currentDir = RotateVector(currentDir, -90); // Turn Left
                    turned = true;
                }
                else if (nextRight == expectedLeft)
                {
                    currentDir = RotateVector(currentDir, 90); // Turn Right
                    turned = true;
                }
                else
                {
                    currentPos = nextPos; // Move Forward
                }

                // Stop: Closed Loop
                if (currentPos == initialPos && currentDir == initialDir && safety > 1)
                {
                    points.Add(initialPos); // Close the loop visually
                    break;
                }
            }

            return points;
        }
        
        public static long HashEdge(Vector2 pos, Vector2 dir)
        {
            int px = (int)pos.X;
            int py = (int)pos.Y;
            int dx = (int)dir.X;
            int dy = (int)dir.Y;

            if (dx != 0) // Horizontal edge
            {
                int edgeX = dx > 0 ? px : px - 1; // Always the left-side x
                int edgeY = py;
                return ((long)edgeX << 32) | (uint)edgeY; // No distinguishing bit needed
            }
            else // Vertical edge (dy != 0)
            {
                int edgeX = px;
                int edgeY = dy > 0 ? py : py - 1; // Always the bottom-side y
                return (1L << 63) | ((long)edgeX << 32) | (uint)edgeY;
            }
        }
        
        private Color GetPixelRelative(Vector2 pos, Vector2 dir, bool isLeft)
        {
            var x = (int)pos.X;
            var y = (int)pos.Y;
            int px = x, py = y;

            // ANCIENT RUNES BE LIKE
            if (dir.X == 0 && dir.Y == -1) { px = isLeft ? x - 1 : x; py = y - 1; }      // UP
            else if (dir.X == 0 && dir.Y == 1) { px = isLeft ? x : x - 1; py = y; }      // DOWN
            else if (dir.X == 1 && dir.Y == 0) { px = x; py = isLeft ? y - 1 : y; }      // RIGHT
            else if (dir.X == -1 && dir.Y == 0) { px = x - 1; py = isLeft ? y : y - 1; } // LEFT

            if (px < 0 || px >= _image.GetWidth() || py < 0 || py >= _image.GetHeight()) 
                return Colors.Black;
            
            return _image.GetPixel(px, py);
        }
        
        private Vector2 RotateVector(Vector2 v, int degrees)
        {
            if (degrees == 90) return new Vector2(-v.Y, v.X);
            if (degrees == -90) return new Vector2(v.Y, -v.X);
            return v;
        }
    }
    
    private static Vector2[] SimplifyPath(Vector2[] points, float tolerance)
    {
        if (points.Length < 3) return points;
        
        var stack = new Stack<int>();
        var keep = new bool[points.Length];
        
        keep[0] = true;
        keep[points.Length - 1] = true;
        stack.Push(points.Length - 1);
        stack.Push(0);

        while (stack.Count > 0)
        {
            var first = stack.Pop();
            var last = stack.Pop();
            var maxDistSq = 0f;
            var indexFarthest = 0;
            
            var lineStart = points[first];
            var lineEnd = points[last];
            var lineVec = lineEnd - lineStart;
            var lineLenSq = lineVec.LengthSquared();

            for (var i = first + 1; i < last; i++)
            {
                var p = points[i];
                float distSq;
                if (lineLenSq == 0) distSq = (p - lineStart).LengthSquared();
                else
                {
                    var t = Mathf.Clamp(((p.X - lineStart.X) * lineVec.X + (p.Y - lineStart.Y) * lineVec.Y) / lineLenSq, 0f, 1f);
                    distSq = (p - (lineStart + lineVec * t)).LengthSquared();
                }

                if (!(distSq > maxDistSq)) continue;
                maxDistSq = distSq;
                indexFarthest = i;
            }

            if (!(maxDistSq > tolerance * tolerance)) continue;
            keep[indexFarthest] = true;
            stack.Push(last);
            stack.Push(indexFarthest);
            stack.Push(indexFarthest);
            stack.Push(first);
        }

        return points.Where((t, i) => keep[i]).ToArray();
    }
    
    private static List<Vector2> RemoveConsecutiveDuplicates(List<Vector2> raw)
    {
        var cleaned = new List<Vector2>();
        foreach (var p in raw)
        {
            if (cleaned.Count == 0 || p != cleaned[cleaned.Count - 1])
                cleaned.Add(p);
        }
        return cleaned;
    }
}