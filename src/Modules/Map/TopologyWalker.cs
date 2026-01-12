using System.Collections.Generic;
using Godot;

namespace ColdStrategyDb.Modules.Map;

public class TopologyWalker
{
    private readonly Image _image;
    // Not readonly anymore - we will inject island nodes into this set
    private readonly HashSet<Vector2> _nodes; 
    private readonly HashSet<(Vector2, Vector2)> _visitedEdges;
    private readonly Vector2[] _cardinals = { Vector2.Right, Vector2.Down, Vector2.Left, Vector2.Up };

    public TopologyWalker(Image image, HashSet<Vector2> nodes)
    {
        _image = image;
        _nodes = nodes; // We use the reference directly to allow injection
        _visitedEdges = new HashSet<(Vector2, Vector2)>();
    }

    public List<BorderData> CollectBorders()
    {
        var borders = new List<BorderData>();
        _visitedEdges.Clear();

        // PHASE 1: Trace Real Junctions
        // We iterate a copy because we might modify the main _nodes set later
        foreach (var node in new List<Vector2>(_nodes)) 
        {
            TraceFromNode(node, borders);
        }

        // PHASE 2: Detect Islands (The "Flood" Scan)
        // We scan for any edge that hasn't been visited yet.
        int w = _image.GetWidth();
        int h = _image.GetHeight();

        for (int y = -1; y < h; y++)
        {
            for (int x = -1; x < w; x++)
            {
                var pos = new Vector2(x, y);
                
                CheckAndInjectIsland(pos, Vector2.Right, borders);
                CheckAndInjectIsland(pos, Vector2.Down, borders);
            }
        }

        return borders;
    }

    private void CheckAndInjectIsland(Vector2 pos, Vector2 dir, List<BorderData> borders)
    {
        // 1. If this specific edge is already visited, skip immediately.
        if (_visitedEdges.Contains((pos, dir))) return;

        // 2. Check if it is a valid border (separates two different colors)
        if (TryGetEdgeColors(pos, dir, out var c1, out var c2) && c1 != c2)
        {
            // FOUND AN ISLAND!
            // This edge exists but wasn't reached by any known node.
            
            // 3. "Spawn" a node here.
            _nodes.Add(pos); 
            
            // 4. Let the standard logic do the rest.
            // Since 'pos' is now in _nodes, the tracer will walk the loop 
            // and stop exactly when it gets back here.
            TraceFromNode(pos, borders);
        }
    }

    private void TraceFromNode(Vector2 node, List<BorderData> borders)
    {
        foreach (var dir in _cardinals)
        {
            if (_visitedEdges.Contains((node, dir))) continue;

            if (TryGetEdgeColors(node, dir, out var c1, out var c2) && c1 != c2)
            {
                var path = TracePath(node, dir, c1, c2);
                if (path != null) 
                {
                    borders.Add(new BorderData 
                    { 
                        Path = path, 
                        ColorLeft = c1, 
                        ColorRight = c2 
                    });
                }
            }
        }
    }

    private Vector2[] TracePath(Vector2 startPos, Vector2 startDir, Color c1, Color c2)
    {
        var path = new List<Vector2> { startPos };
        var currPos = startPos;
        var currDir = startDir;

        MarkVisited(currPos, currDir);

        while (true)
        {
            currPos += currDir;
            path.Add(currPos);

            // STOP CONDITION: We hit ANY node (Real or Injected)
            if (_nodes.Contains(currPos))
            {
                MarkVisited(currPos, -currDir);
                return path.ToArray();
            }

            // Standard Navigation Logic
            var found = false;
            var nextDirs = new[] { currDir, TurnLeft(currDir), TurnRight(currDir) };

            foreach (var nextDir in nextDirs)
            {
                if (IsValidContinuation(currPos, nextDir, c1, c2))
                {
                    MarkVisited(currPos, nextDir);
                    currDir = nextDir;
                    found = true;
                    break;
                }
            }

            if (!found) return null; // Dead end
        }
    }

    // --- Standard Helpers ---

    private void MarkVisited(Vector2 pos, Vector2 dir)
    {
        _visitedEdges.Add((pos, dir));
        _visitedEdges.Add((pos + dir, -dir));
    }

    private bool TryGetEdgeColors(Vector2 node, Vector2 dir, out Color cLeft, out Color cRight)
    {
        cLeft = default; cRight = default;
        int x = (int)node.X; int y = (int)node.Y;

        if (dir == Vector2.Right) {
            cLeft = GetPixelSafe(x + 1, y);      
            cRight = GetPixelSafe(x + 1, y + 1); 
        }
        else if (dir == Vector2.Down) {
            cLeft = GetPixelSafe(x + 1, y + 1);  
            cRight = GetPixelSafe(x, y + 1);     
        }
        else if (dir == Vector2.Left) {
            cLeft = GetPixelSafe(x, y + 1);      
            cRight = GetPixelSafe(x, y);         
        }
        else if (dir == Vector2.Up) {
            cLeft = GetPixelSafe(x, y);          
            cRight = GetPixelSafe(x + 1, y);     
        }
        else return false;
        
        return true;
    }

    private bool IsValidContinuation(Vector2 node, Vector2 dir, Color tA, Color tB)
    {
        if (TryGetEdgeColors(node, dir, out var c1, out var c2))
        {
            return (c1 == tA && c2 == tB) || (c1 == tB && c2 == tA);
        }
        return false;
    }

    private Color GetPixelSafe(int x, int y)
    {
        if (x < 0 || x >= _image.GetWidth() || y < 0 || y >= _image.GetHeight())
            return Colors.Transparent;
        return _image.GetPixel(x, y);
    }

    private Vector2 TurnLeft(Vector2 dir) => new Vector2(dir.Y, -dir.X);
    private Vector2 TurnRight(Vector2 dir) => new Vector2(-dir.Y, dir.X);
}