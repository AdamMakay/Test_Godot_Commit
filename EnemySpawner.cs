using Godot;

public partial class EnemySpawner : Node2D
{
    [Export] public PackedScene EnemyScene;
    [Export] public Marker2D LeftSpawn;
    [Export] public Marker2D RightSpawn;

    [Export] public float StartInterval = 3.0f;
    [Export] public float MinInterval = 0.4f;
    [Export] public float Decay = 0.97f;
    [Export] public float DoubleSpawnAfter = 60f;   // új

    private Timer _timer;
    private float _interval;
    private float _elapsed;                         // új
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        _interval = StartInterval;

        _timer = new Timer { OneShot = true };
        AddChild(_timer);
        _timer.Timeout += OnTimeout;
        _timer.Start(_interval);
    }

    public override void _Process(double delta)     // új
    {
        _elapsed += (float)delta;
    }

    private void OnTimeout()
    {
        SpawnEnemy();

        if (DoubleSpawnAfter > 0 && _elapsed > DoubleSpawnAfter)   // új
            SpawnEnemy();

        _interval = Mathf.Max(MinInterval, _interval * Decay);
        _timer.Start(_interval);
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

        enemy.Modulate = new Color(1, 1, 1, 0);                     // új
        var tween = enemy.CreateTween();                            // új
        tween.TweenProperty(enemy, "modulate:a", 1.0f, 0.4f);       // új
    }
}