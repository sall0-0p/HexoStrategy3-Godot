using Godot;

public partial class DebugCamera : Camera2D
{
    [Export] public float MoveSpeed = 1000.0f;
    [Export] public float ZoomSpeed = 5.0f; // Increased because we multiply by delta now
    [Export] public float MinZoom = 0.5f;
    [Export] public float MaxZoom = 10f;

    public override void _Process(double delta)
    {
        // --- Movement ---
        var velocity = Vector2.Zero;

        if (Input.IsKeyPressed(Key.W)) velocity.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) velocity.Y += 1;
        if (Input.IsKeyPressed(Key.A)) velocity.X -= 1;
        if (Input.IsKeyPressed(Key.D)) velocity.X += 1;

        if (velocity.Length() > 0)
            velocity = velocity.Normalized();
        
        float speedMultiplier = Input.IsKeyPressed(Key.Shift) ? 2.5f : 1.0f;
        Position += velocity * MoveSpeed * speedMultiplier * (float)delta;

        // --- Zoom (Q / E) ---
        float zoomChange = 0.0f;
        if (Input.IsKeyPressed(Key.E)) zoomChange += 1; // Zoom In
        if (Input.IsKeyPressed(Key.Q)) zoomChange -= 1; // Zoom Out

        if (zoomChange != 0)
        {
            var newZoom = Zoom + new Vector2(zoomChange, zoomChange) * ZoomSpeed * (float)delta;
            
            // Apply clamp
            Zoom = new Vector2(
                Mathf.Clamp(newZoom.X, MinZoom, MaxZoom),
                Mathf.Clamp(newZoom.Y, MinZoom, MaxZoom)
            );
        }
    }
}