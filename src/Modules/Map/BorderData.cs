using Godot;

namespace ColdStrategyDb.Modules.Map;

public struct BorderData
{
    public Color ColorLeft { get; set; }
    public Color ColorRight { get; set; }
    public Vector2[] Path { get; set; }
}