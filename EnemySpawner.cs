using Godot;

public partial class EnemySpawner : Node2D
{
    [Export] public PackedScene EnemyScene;
    [Export] public Marker2D LeftSpawn;
    [Export] public Marker2D RightSpawn;

    [Export] public float StartInterval = 3.0f;

    private Timer _timer;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();

        _timer = new Timer { OneShot = true };
        AddChild(_timer);
        _timer.Timeout += OnTimeout;
        _timer.Start(StartInterval);
    }

    private void OnTimeout()
    {
        SpawnEnemy();
        _timer.Start(StartInterval);
    }

    private void SpawnEnemy()
    {
        if (EnemyScene == null || LeftSpawn == null || RightSpawn == null)
        {
            GD.PrintErr("EnemySpawner: hiányzó EnemyScene vagy spawn marker!");
            return;
        }

        bool left = _rng.Randf() < 0.5f;
        Marker2D point = left ? LeftSpawn : RightSpawn;

        var enemy = EnemyScene.Instantiate<Node2D>();
        enemy.GlobalPosition = point.GlobalPosition;
        GetParent().AddChild(enemy);
    }
}