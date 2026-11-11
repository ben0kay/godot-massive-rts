// Smooth ground-plane pan and wheel zoom for the benchmark's orthographic camera.
using Godot;
namespace MassiveRts;

public partial class IsometricCamera : Camera3D
{
    private Vector3 focus = Vector3.Zero;
    private Vector3 velocity;
    private float targetSize = 180;
    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal; Size = targetSize; Far = 6000; Reposition();
    }
    // =========================================================
    public void Frame(float battlefieldSize) { targetSize = Mathf.Clamp(battlefieldSize, 30, 1800); }
    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventMouseButton mouse && mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.WheelUp) targetSize = Mathf.Max(15, targetSize * 0.88f);
            if (mouse.ButtonIndex == MouseButton.WheelDown) targetSize = Mathf.Min(2000, targetSize / 0.88f);
        }
    }
    // =========================================================
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Vector2 input = new((Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0),
            (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0));
        Vector3 right = new Vector3(1, 0, -1).Normalized(), back = new Vector3(1, 0, 1).Normalized();
        Vector3 desired = (right * input.X + back * input.Y).LimitLength() * Size;
        velocity = velocity.Lerp(desired, 1 - Mathf.Exp(-10 * dt)); focus += velocity * dt;
        Size = Mathf.Lerp(Size, targetSize, 1 - Mathf.Exp(-10 * dt)); Reposition();
    }
    private void Reposition() { Position = focus + new Vector3(700, 850, 700); LookAt(focus, Vector3.Up); }
}
