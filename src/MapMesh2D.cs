using ColdStrategyDb.Modules.Map;
using Godot;

public partial class MapMesh2D : Node2D
{
    private MapData _debugData;
    public override void _Ready()
    {
        var image = GetNode<Sprite2D>("MapSprite").Texture.GetImage();
        var data = MapScanner.Scan(image, 15f);
        GD.Print(data.Borders.Count);
        _DrawDebugLines(data);
    }
    
    private void _DrawDebugLines(MapData data)
    {
        _debugData = data;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_debugData?.Borders == null) return;
        
        var texture = GetNode<Sprite2D>("MapSprite").Texture; 
        var offset = new Vector2(-texture.GetWidth() / 2.0f, -texture.GetHeight() / 2.0f);
        DrawSetTransform(offset, 0f, Vector2.One);
        
        foreach (var border in _debugData.Borders)
        {
            if (border.Points.Length > 1)
                DrawPolyline(border.Points, Colors.Red, 2.0f);
        }
    }
}