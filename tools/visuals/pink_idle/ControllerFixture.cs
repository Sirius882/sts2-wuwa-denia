using Godot;

namespace Denia;

// The production controller is source-linked by run_preview.py; only game services are stand-ins.
public sealed class PreviewEntity { public bool Black, IsDead, Dying; public NCreature Node = null!; }
public partial class NCreature : Node2D
{
    public PreviewEntity Entity = new();
    public Node2D Visuals = null!, Body = new();
    public TextureRect Pink = null!, Black = null!;
    public DeniaFormOverlaySync Sync = null!;
    public Vector2 LayoutPosition = new(262, 117), LayoutSize = new(226, 350);
}
public static class DeniaFormHelper { public static bool IsBlack(PreviewEntity entity) => entity.Black; }
public static class DeniaFormPatch
{
    public const float PinkDeathDuration = 2;
    public static bool Fast;
    public static bool IsFastMode() => Fast;
    public static bool IsDying(NCreature node) => node.Entity.Dying;
    public static void SetDying(NCreature node, bool value) => node.Entity.Dying = value;
    public static bool IsBlackFormAttackCardActive(PreviewEntity entity) => false;
    public static void RefreshForCreature(PreviewEntity entity) => entity.Node.Sync.SetForm(entity.Black);
    public static void SyncFacing(NCreature node)
    {
        foreach (var overlay in new[] { node.Pink, node.Black })
        {
            bool flip = node.Body.Scale.X < 0;
            overlay.GetNode<AnimatedSprite2D>("Body/Idle").FlipH = flip;
            overlay.GetNode<DeniaFlowIdleVisual>("Body/RealtimeIdle").SetFacing(flip);
            overlay.GetNodeOrNull<DeniaSingleHitVisual>(DeniaSingleHitVisual.NodeName)?.SetFacing(flip);
        }
    }
    public static void SyncOverlayLayout(NCreature node)
    {
        var size = node.LayoutSize;
        foreach (var overlay in new[] { node.Pink, node.Black })
        {
            var actions = overlay.GetNode<AnimatedSprite2D>("Body/Idle");
            actions.Position = size * 0.5f;
            actions.Scale = Vector2.One * (size.Y / 512);
            foreach (string name in new[] { "Body/RealtimeIdle", DeniaPinkAttackVisual.NodeName })
            {
                var visual = overlay.GetNodeOrNull<DeniaFlowIdleVisual>(name);
                if (visual == null) continue;
                visual.Position = new Vector2(size.X * 0.5f, size.Y);
                visual.Scale = Vector2.One * (size.Y / visual.LayoutHeight);
            }
        }
        node.Sync.SetBaseLayout(node.LayoutPosition, size);
        SyncFacing(node);
    }
}
public static class ControllerFixture
{
    public static NCreature Create(Node parent)
    {
        var node = new NCreature();node.Entity.Node = node;
        parent.AddChild(node);
        node.Visuals = GD.Load<PackedScene>("res://scenes/creature_visuals/denia_pink.tscn").Instantiate<Node2D>();
        node.AddChild(node.Visuals);node.AddChild(node.Body);
        foreach (var name in new[] { "Visuals", "PhobiaModeVisuals" })
            node.Visuals.GetNodeOrNull<Node2D>(name)?.Hide();
        node.Pink = Build(node.Visuals, "PinkVisuals", new DeniaPinkIdleVisual());
        node.Black = Build(node.Visuals, "DarkVisuals", new DeniaBlackIdleVisual());
        node.Sync = new DeniaFormOverlaySync();node.Visuals.AddChild(node.Sync);
        node.Sync.Bind(node, node.Pink, node.Black, null, null);
        node.Sync.SetProcess(false);
        node.Sync.SetForm(false);
        DeniaFormPatch.SyncOverlayLayout(node);
        return node;
    }
    private static TextureRect Build(Node2D visuals, string name, DeniaFlowIdleVisual idle)
    {
        var overlay = new TextureRect { MouseFilter = Control.MouseFilterEnum.Ignore };
        visuals.AddChild(overlay);
        var body = new Node2D { Name = "Body" };overlay.AddChild(body);
        var actions = visuals.GetNode<AnimatedSprite2D>(name);
        actions.Reparent(body, false);actions.Name = "Idle";actions.Show();actions.Play("idle");
        actions.AnimationFinished += () => {
            if (actions.Animation == "attack" || actions.Animation == "dodge") { actions.SpeedScale = 1;actions.Play("idle"); }
        };
        idle.Name = "RealtimeIdle";body.AddChild(idle);idle.BindActions(actions);
        return overlay;
    }
}
