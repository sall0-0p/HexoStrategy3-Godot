using Godot;

public partial class HelloWorld : Node2D
{
    public override void _Ready()
    {
        GD.Print("Hello world from root node!");
    }
}