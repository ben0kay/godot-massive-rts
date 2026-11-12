// Orthographic camera with adjustable battlefield yaw, screen-relative pan and wheel zoom.
using Godot;
namespace MassiveRts;

public partial class IsometricCamera : Camera3D
{
    private Vector3 focus = Vector3.Zero;
    private Vector3 velocity;
    private float targetSize = 180;
    private float yawDegrees = 45f;
    private Vector3 screenRight, screenBack, orbitOffset;

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal; Size = targetSize; Far = 6000;
        SetYawDegrees(yawDegrees);
    }

    // =========================================================
    // 0° looks along the world Z axis, while 45° reproduces the original diagonal.
    // Recalculate pan axes only when the user changes camera rotation.
    public void SetYawDegrees(float degrees)
    {
        if (!float.IsFinite(degrees)) return;
        yawDegrees = Mathf.PosMod(degrees, 360f);
        float radians = Mathf.DegToRad(yawDegrees);
        float sine = Mathf.Sin(radians), cosine = Mathf.Cos(radians);
        screenRight = new Vector3(cosine, 0f, -sine);
        screenBack = new Vector3(sine, 0f, cosine);
        // Original offset was (700, 850, 700); preserve radius and viewing pitch.
        orbitOffset = screenBack * 989.9495f + Vector3.Up * 850f;
        Reposition();
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
        Vector3 desired = (screenRight * input.X + screenBack * input.Y).LimitLength() * Size;
        velocity = velocity.Lerp(desired, 1 - Mathf.Exp(-10 * dt)); focus += velocity * dt;
        Size = Mathf.Lerp(Size, targetSize, 1 - Mathf.Exp(-10 * dt)); Reposition();
    }
    private void Reposition() { Position = focus + orbitOffset; LookAt(focus, Vector3.Up); }
}
