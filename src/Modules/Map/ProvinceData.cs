using System.Collections.Generic;
using Godot;

namespace ColdStrategyDb.Modules.Map;

public class ProvinceData
{
    public Color IdColor { get; set; }
    public List<Vector2[]> Polygons { get; set; } = [];
    public HashSet<Color> Neighbors { get; set; } = [];
    public Vector2 Center { get; set; }
    public Rid VisualRid { get; set; }
}