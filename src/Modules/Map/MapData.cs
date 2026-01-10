using System.Collections.Generic;
using Godot;

namespace ColdStrategyDb.Modules.Map;

public class MapData
{
    public int Width { get; set; }
    public int Height { get; set; }
    
    public Dictionary<Color, ProvinceData> Provinces { get; set; } = new();
    public List<BorderData> Borders { get; set; } = [];
}