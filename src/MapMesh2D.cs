using System.Collections.Generic;
using ColdStrategyDb.Modules.Map;
using Godot;

public partial class MapMesh2D : Node2D
{
    private MapData _debugData;
    private HashSet<Vector2> _debugNodes;
    private List<Vector2[]> _debugBorders;
    public override void _Ready()
    {
        var image = GetNode<Sprite2D>("MapSprite").Texture.GetImage();
        var rawData = MapScanner.Scan(image);
        _debugNodes = MapScanner.DebugNodes;
        _debugBorders = MapScanner.DebugBorders;
        
        MapBuilder.BuildProvinces(rawData, this.GetCanvasItem());
        // var parisData = rawData.Provinces[];
        // GD.Print($"Neighbors: {parisData.Neighbors.Count}");
        // RenderingServer.CanvasItemSetModulate(parisData.VisualRid, Colors.White);
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
            if (border.Path.Length > 1)
                DrawPolyline(border.Path, Colors.Red, 2.0f);
        }
    
        foreach (var node in _debugNodes)
        {
            var color = Colors.Black;
            color.A = 0.5f;
            DrawRect(new Rect2(node.X, node.Y, new Vector2(2, 2)), color);
        }
    
        GD.Print(_debugBorders.Count);
        foreach (var border in _debugBorders)
        {
            var color = Colors.Red;
            color.A = 0.5f;
    
            foreach (var pixel in border)
            {
                DrawRect(new Rect2(pixel, new Vector2(1, 1)), color);
            }
        }
    }
}