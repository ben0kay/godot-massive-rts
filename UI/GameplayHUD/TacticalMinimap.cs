// Draws a cached terrain map, bounded density overlay and camera footprint.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class TacticalMinimap : Control
{
    private const int TerrainResolution = 192, DensityResolution = 48;
    private static readonly Color Water = new("203f50"), Cover = new("59606a"), Bridge = new("888b83");
    private readonly int[] density = new int[DensityResolution * DensityResolution * Factions.Count];
    private readonly Color[] colors = new Color[Factions.Count];
    private readonly Vector2[] cameraOutline = new Vector2[5];
    private readonly List<(Vector2 Position, byte Faction)> airborne = new();
    private readonly List<(Vector2 Position, byte Faction, bool Citadel)> buildings = new();
    private readonly Vector2[] corners = new Vector2[4];
    private readonly Vector2[] frame = new Vector2[6];
    private readonly Vector2[] frameOutline = new Vector2[7];
    private ImageTexture terrainTexture, densityTexture;
    private Image densityImage;
    private Rect2 world;
    private Rect2 MapRect => new(new Vector2(7, 7), Size - new Vector2(14, 14));
    private TerrainHeightField terrain;
    private IsometricCamera camera;
    private Transform3D previousTransform;
    private Vector2 previousViewport;
    private float previousZoom;
    private bool footprint, dragging;

    internal TacticalMinimap()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(220, 220);
        for (int i = 0; i < colors.Length; i++) colors[i] = Factions.Color(i);
        Resized += () =>
        {
            previousZoom = 0;
            frame[0] = new Vector2(0, 9);
            frame[1] = new Vector2(9, 0);
            frame[2] = new Vector2(Size.X, 0);
            frame[3] = new Vector2(Size.X, Size.Y - 9);
            frame[4] = new Vector2(Size.X - 9, Size.Y);
            frame[5] = new Vector2(0, Size.Y);
            frame.CopyTo(frameOutline, 0);
            frameOutline[6] = frame[0];
            QueueRedraw();
        };
        VisibilityChanged += () => { dragging = false; SetProcess(IsVisibleInTree() && camera != null); };
        SetProcess(false);
    }

    // =========================================================
    internal void Configure(Rect2 bounds, TerrainHeightField field, ReadOnlySpan<ObstacleRectangle> water,
        ReadOnlySpan<ObstacleRectangle> cover, Rect2[] bridges, IsometricCamera view)
    {
        world = bounds;
        terrain = field;
        camera = view;
        previousZoom = 0;
        CustomMinimumSize = bounds.Size / Mathf.Max(bounds.Size.X, bounds.Size.Y) * 206 + new Vector2(14, 14);
        using var image = Image.CreateEmpty(TerrainResolution, TerrainResolution, false, Image.Format.Rgba8);
        for (int y = 0; y < TerrainResolution; y++)
        for (int x = 0; x < TerrainResolution; x++)
        {
            var point = bounds.Position + (new Vector2(x + .5f, y + .5f) / TerrainResolution) * bounds.Size;
            float height = field?.At(point.X, point.Y) ?? 0f;
            float slope = field == null ? 0 : Mathf.Abs(height - field.At(point.X + bounds.Size.X / TerrainResolution, point.Y));
            var shade = new Color("263339").Lightened(Mathf.Clamp(height * .007f, -.11f, .13f));
            image.SetPixel(x, y, shade.Lerp(Cover, Mathf.Clamp(slope * .045f, 0, .7f)));
        }
        foreach (var obstacle in cover) Stamp(image, new Rect2(obstacle.Min, obstacle.Max - obstacle.Min), Cover);
        foreach (var area in water) Stamp(image, new Rect2(area.Min, area.Max - area.Min), Water);
        if (bridges != null)
            foreach (var bridge in bridges) Stamp(image, bridge, Bridge);
        terrainTexture = ImageTexture.CreateFromImage(image);
        densityImage = Image.CreateEmpty(DensityResolution, DensityResolution, false, Image.Format.Rgba8);
        densityTexture = ImageTexture.CreateFromImage(densityImage);
        SetProcess(IsVisibleInTree());
        QueueRedraw();
    }

    private void Stamp(Image image, Rect2 area, Color color)
    {
        var start = (area.Position - world.Position) / world.Size * TerrainResolution;
        var end = (area.End - world.Position) / world.Size * TerrainResolution;
        int left = Mathf.Clamp(Mathf.FloorToInt(start.X), 0, TerrainResolution);
        int right = Mathf.Clamp(Mathf.CeilToInt(end.X), 0, TerrainResolution);
        int top = Mathf.Clamp(Mathf.FloorToInt(start.Y), 0, TerrainResolution);
        int bottom = Mathf.Clamp(Mathf.CeilToInt(end.Y), 0, TerrainResolution);
        if (right > left && bottom > top) image.FillRect(new Rect2I(left, top, right - left, bottom - top), color);
    }

    // =========================================================
    internal void Refresh(UnitState units, AirSquadron air, IReadOnlyList<StructureHealth> structures, StructureHealth objective, byte viewer)
    {
        if (!IsVisibleInTree() || densityImage == null) return;
        Array.Clear(density);
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0f) continue;
            byte faction = units.Faction[id];
            var uv = UnitUv(units.Position[id]);
            if (uv.X < 0 || uv.X >= 1 || uv.Y < 0 || uv.Y >= 1) continue;
            if (faction != viewer && !units.CanDetectGroundTargetFromOutsideTrench(id)) continue;
            int cell = (int)(uv.Y * DensityResolution) * DensityResolution + (int)(uv.X * DensityResolution);
            density[cell * Factions.Count + faction]++;
        }
        for (int y = 0; y < DensityResolution; y++)
        for (int x = 0; x < DensityResolution; x++)
        {
            int offset = (y * DensityResolution + x) * Factions.Count;
            Color color = Colors.Transparent;
            int total = 0;
            for (int faction = 0; faction < Factions.Count; faction++)
            {
                int count = density[offset + faction];
                if (count == 0) continue;
                total += count;
                color = color.Lerp(colors[faction], count / (float)total);
            }
            densityImage.SetPixel(x, y, new Color(color, total == 0 ? 0 : Mathf.Min(.95f, .52f + .09f * Mathf.Sqrt(total))));
        }
        densityTexture.Update(densityImage);
        airborne.Clear();
        if (air != null)
            foreach (ref readonly var craft in air.TacticalAircraft)
                if (craft.Health > 0f) airborne.Add((UnitUv(craft.Position), craft.Faction));
        buildings.Clear();
        if (structures != null)
            foreach (var structure in structures)
                if (!structure.Destroyed)
                    buildings.Add((((structure.Area.Min + structure.Area.Max) * .5f - world.Position) / world.Size,
                        structure.Faction, ReferenceEquals(structure, objective)));
        QueueRedraw();
    }

    private Vector2 UnitUv(Vector3 point) => (ObstacleField.Xz(point) - world.Position) / world.Size;
    private Vector2 OnMap(Vector2 uv) => MapRect.Position + uv * MapRect.Size;

    // =========================================================
    public override void _Process(double delta)
    {
        Vector2 viewport = camera.GetViewport().GetVisibleRect().Size;
        if (previousTransform == camera.GlobalTransform && previousViewport == viewport && previousZoom == camera.Size) return;
        previousTransform = camera.GlobalTransform;
        previousViewport = viewport;
        previousZoom = camera.Size;
        corners[1] = new Vector2(viewport.X, 0);
        corners[2] = viewport;
        corners[3] = new Vector2(0, viewport.Y);
        footprint = true;
        for (int i = 0; i < corners.Length; i++)
        {
            var origin = camera.ProjectRayOrigin(corners[i]);
            var ray = camera.ProjectRayNormal(corners[i]);
            if (ray.Y >= -.0001f) { footprint = false; break; }
            var point = origin - ray * (origin.Y / ray.Y);
            if (terrain != null)
                for (int sample = 0; sample < 4; sample++)
                    point = origin + ray * ((terrain.At(point.X, point.Z) - origin.Y) / ray.Y);
            cameraOutline[i] = OnMap(UnitUv(point).Clamp(Vector2.Zero, Vector2.One));
        }
        cameraOutline[4] = cameraOutline[0];
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (terrainTexture == null || Size.X < 14 || Size.Y < 14) return;
        DrawColoredPolygon(frame, new Color("101a25"));
        DrawPolyline(frameOutline, TacticalHudStyle.Silver, 1f, true);
        DrawTextureRect(terrainTexture, MapRect, false);
        DrawTextureRect(densityTexture, MapRect, false);
        foreach (var building in buildings)
        {
            if (!new Rect2(Vector2.Zero, Vector2.One).HasPoint(building.Position)) continue;
            var position = OnMap(building.Position);
            float radius = building.Citadel ? 4 : 2;
            var marker = new Rect2(position - Vector2.One * radius, Vector2.One * 2 * radius);
            DrawRect(marker, colors[building.Faction]);
            if (building.Citadel) DrawRect(marker, Colors.White, false);
        }
        foreach (var aircraft in airborne)
        {
            if (!new Rect2(Vector2.Zero, Vector2.One).HasPoint(aircraft.Position)) continue;
            var point = OnMap(aircraft.Position);
            DrawLine(point + new Vector2(-2, 2), point + new Vector2(0, -2), colors[aircraft.Faction], 1f);
            DrawLine(point + new Vector2(0, -2), point + new Vector2(2, 2), colors[aircraft.Faction], 1f);
        }
        if (footprint) DrawPolyline(cameraOutline, new Color(Colors.White, .82f), 1f, true);
    }

    public override void _GuiInput(InputEvent input)
    {
        if (camera == null) return;
        if (input is InputEventMouseButton button)
        {
            AcceptEvent();
            if (button.ButtonIndex != MouseButton.Left) return;
            dragging = button.Pressed && MapRect.HasPoint(button.Position);
            if (dragging) Pan(button.Position);
        }
        else if (dragging && input is InputEventMouseMotion motion)
        {
            AcceptEvent();
            dragging = (motion.ButtonMask & MouseButtonMask.Left) != 0;
            if (dragging) Pan(motion.Position);
        }
    }

    private void Pan(Vector2 position)
    {
        var uv = ((position - MapRect.Position) / MapRect.Size).Clamp(Vector2.Zero, Vector2.One);
        var target = world.Position + uv * world.Size;
        camera.PanTo(new Vector3(target.X, terrain?.At(target.X, target.Y) ?? 0, target.Y));
    }

    internal void Reset()
    {
        SetProcess(false);
        camera = null;
        terrain = null;
        terrainTexture = densityTexture = null;
        densityImage = null;
        footprint = dragging = false;
        airborne.Clear();
        buildings.Clear();
    }
}
