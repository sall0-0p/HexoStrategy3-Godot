using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ColdStrategyDb.Modules.Map;

public static class MapBuilder
{
    /// <summary>
    /// Processes raw borders into ProvinceData, calculates neighbors/centers, 
    /// and generates visuals via RenderingServer.
    /// </summary>
    /// <param name="mapData">The MapData object to populate.</param>
    /// <param name="parentCanvasItem">The RID of the parent node (e.g. the Map View node) to attach visuals to.</param>
    public static void BuildProvinces(MapData mapData, Rid parentCanvasItem)
    {
        // 1. Group segments by Color to process one province at a time
        var provinceSegments = new Dictionary<Color, List<Vector2[]>>();
        var provinceNeighbors = new Dictionary<Color, HashSet<Color>>();

        foreach (var border in mapData.Borders)
        {
            // --- Logic for the 'Left' Province ---
            AddSegment(provinceSegments, border.ColorLeft, border.Path);
            RegisterNeighbor(provinceNeighbors, border.ColorLeft, border.ColorRight);

            // --- Logic for the 'Right' Province ---
            // Reverse path to maintain Counter-Clockwise winding order
            var reversedPath = new Vector2[border.Path.Length];
            int len = border.Path.Length;
            for(int i=0; i<len; i++) reversedPath[i] = border.Path[len - 1 - i];
            
            AddSegment(provinceSegments, border.ColorRight, reversedPath);
            RegisterNeighbor(provinceNeighbors, border.ColorRight, border.ColorLeft);
        }

        // 2. Build Data and Visuals for each Province
        foreach (var (color, segments) in provinceSegments)
        {
            // Skip transparent/void colors if your map has them
            if (color.A == 0) continue;

            var province = new ProvinceData
            {
                IdColor = color,
                Neighbors = provinceNeighbors.ContainsKey(color) ? provinceNeighbors[color] : new HashSet<Color>()
            };

            // Stitch raw segments into closed loops
            province.Polygons = StitchPolygons(segments);
            
            // Calculate Center (Centroid)
            province.Center = CalculateCentroid(province.Polygons);

            // Create Visuals using RenderingServer
            province.VisualRid = CreateProvinceVisual(parentCanvasItem, province.Polygons, color);

            mapData.Provinces[color] = province;
        }
    }

    private static Rid CreateProvinceVisual(Rid parent, List<Vector2[]> polygons, Color color)
    {
        // Create a new CanvasItem directly on the Server
        var itemRid = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(itemRid, parent);

        // We will combine all polygons of this province into one visual command
        // Note: For complex concave shapes, Godot's RenderingServer works best with triangles.
        
        var allVerts = new List<Vector2>();
        var allColors = new List<Color>();
        // Using indices is often more efficient for the GPU than duplicating verts
        // but for 2D filled polygons, Godot's simple API 'CanvasItemAddPolygon' is easiest,
        // OR we manually triangulate for perfect control.
        
        // Strategy: Pre-triangulate. It's safer for complex map shapes.
        var finalVerts = new List<Vector2>();
        
        foreach (var poly in polygons)
        {
            // Triangulate
            var indices = Geometry2D.TriangulatePolygon(poly);
            
            if (indices.Length == 0) continue;

            for (int i = 0; i < indices.Length; i++)
            {
                finalVerts.Add(poly[indices[i]]);
            }
        }
        
        var colors = new Color[finalVerts.Count];
        System.Array.Fill(colors, color);

        // Push the draw command
        RenderingServer.CanvasItemAddTriangleArray(itemRid, indices: null, points: finalVerts.ToArray(), colors: colors);

        return itemRid;
    }

    // --- Helper Logic (Stitching & Math) ---

    private static void AddSegment(Dictionary<Color, List<Vector2[]>> dict, Color id, Vector2[] segment)
    {
        if (!dict.ContainsKey(id)) dict[id] = new List<Vector2[]>();
        dict[id].Add(segment);
    }

    private static void RegisterNeighbor(Dictionary<Color, HashSet<Color>> dict, Color me, Color neighbor)
    {
        if (neighbor.A == 0) return; // Don't register void as a neighbor
        if (!dict.ContainsKey(me)) dict[me] = new HashSet<Color>();
        dict[me].Add(neighbor);
    }

    private static Vector2 CalculateCentroid(List<Vector2[]> polygons)
    {
        // Simple weighting by vertex count or bounding box is usually enough for map labels
        // Weighted average of all points:
        Vector2 sum = Vector2.Zero;
        int count = 0;
        
        // Ideally we should weight by polygon Area, but this is a fast approximation
        // that usually lands inside the largest landmass of the province.
        foreach (var poly in polygons)
        {
            foreach (var p in poly)
            {
                sum += p;
                count++;
            }
        }
        return count > 0 ? sum / count : Vector2.Zero;
    }

    private static List<Vector2[]> StitchPolygons(List<Vector2[]> segments)
    {
        var closedLoops = new List<Vector2[]>();
        // Using a HashSet for fast "contains" check
        var segmentsPool = new HashSet<Vector2[]>(segments);
        
        // Lookup to find "Who starts at point X?" instantly
        var startsAt = segments.ToLookup(s => s[0]);

        while (segmentsPool.Count > 0)
        {
            var currentSeg = segmentsPool.First();
            segmentsPool.Remove(currentSeg);

            var currentLoop = new List<Vector2>(currentSeg);
            var currentEnd = currentSeg[^1];

            // Keep finding the next segment until we loop back
            while (currentEnd != currentLoop[0])
            {
                // Find a segment that starts where we ended
                var nextSeg = startsAt[currentEnd].FirstOrDefault(s => segmentsPool.Contains(s));

                if (nextSeg == null) break; // Should not happen in closed topology

                segmentsPool.Remove(nextSeg);

                // Add points (skip first one as it overlaps)
                for (int i = 1; i < nextSeg.Length; i++)
                {
                    currentLoop.Add(nextSeg[i]);
                }
                currentEnd = nextSeg[^1];
            }
            closedLoops.Add(currentLoop.ToArray());
        }
        return closedLoops;
    }
}