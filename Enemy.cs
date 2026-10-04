using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public float Speed = 80f;
    [Export] public float Gravity = 1200f;
    [Export] public float AttackRange = 50f;      // ilyen közelről üt
    [Export] public float AttackCooldown = 1.2f;  // két ütés között (mp)
    [Export] public int Damage = 1;
    [Export] public int HitFrame = 3;             // az ütés melyik képkockánál sebez
    [Export] public bool SpriteFacesRight = true; // a sprite alapból jobbra néz?

    [Export] public string IdleAnim = "idle";
    [Export] public string WalkAnim = "walk";
    [Export] public string AttackAnim = "attack";

    private AnimatedSprite2D _sprite;
    private Node2D _player;
    private float _spriteBaseX;
    private float _cooldown;
    private bool _attacking;
    private bool _hitDone;
    private int _facing = 1; // 1 = jobbra, -1 = balra

    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _spriteBaseX = _sprite.Position.X;
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        // az ütés animáció ne ismétlődjön, különben nem jön AnimationFinished
        if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(AttackAnim))
            _sprite.SpriteFrames.SetAnimationLoop(AttackAnim, false);

        _sprite.AnimationFinished += OnAnimationFinished;
        _sprite.FrameChanged += OnFrameChanged;

        PlayAnim(IdleAnim);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector2 vel = Velocity;

        // gravitáció
        if (!IsOnFloor())
            vel.Y += Gravity * dt;
        else
            vel.Y = 0;

        if (_player == null)
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        _cooldown -= dt;

        if (_player != null)
        {
            float dx = _player.GlobalPosition.X - GlobalPosition.X;

            // fordulás a játékos felé (ütés közben nem fordul meg)
            if (!_attacking && Mathf.Abs(dx) > 5f)
            {
                _facing = dx > 0 ? 1 : -1;
                ApplyFacing();
            }

            if (_attacking)
            {
                vel.X = 0;
            }
            else if (Mathf.Abs(dx) > AttackRange)
            {
                vel.X = _facing * Speed;
                PlayAnim(WalkAnim);
            }
            else
            {
                vel.X = 0;
                if (_cooldown <= 0)
                    StartAttack();
                else
                    PlayAnim(IdleAnim);
            }
        }
        else
        {
            vel.X = 0;
            PlayAnim(IdleAnim);
        }

        Velocity = vel;
        MoveAndSlide();
    }

    private void ApplyFacing()
    {
        // a sprite tükrözése scale-lel, az X eltolást is tükrözzük,
        // így a kard miatti oldalirányú eltolás nem ugrik át a másik oldalra
        float s = _facing * (SpriteFacesRight ? 1f : -1f);
        _sprite.Scale = new Vector2(s, 1f);
        _sprite.Position = new Vector2(_spriteBaseX * s, _sprite.Position.Y);
    }

    private void StartAttack()
    {
        _attacking = true;
        _hitDone = false;
        PlayAnim(AttackAnim, true);
    }

    private void OnFrameChanged()
    {
        if (_attacking && !_hitDone && _sprite.Animation == AttackAnim && _sprite.Frame >= HitFrame)
        {
            _hitDone = true;
            DealDamage();
        }
    }

    private void DealDamage()
    {
        if (_player == null) return;

        float dx = _player.GlobalPosition.X - GlobalPosition.X;
        bool inFront = Mathf.Sign(dx) == _facing;

        if (inFront && Mathf.Abs(dx) <= AttackRange * 1.3f && _player.HasMethod("TakeDamage"))
            _player.Call("TakeDamage", Damage);
    }

    private void OnAnimationFinished()
    {
        if (_attacking)
        {
            _attacking = false;
            _cooldown = AttackCooldown;
        }
    }

    private void PlayAnim(string name, bool restart = false)
    {
        if (_sprite.SpriteFrames == null || !_sprite.SpriteFrames.HasAnimation(name)) return;
        if (restart || _sprite.Animation != name || !_sprite.IsPlaying())
            _sprite.Play(name);
    }
}