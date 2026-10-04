using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public float Speed = 80f;
    [Export] public float Gravity = 1200f;
    [Export] public float AttackRange = 50f;
    [Export] public float AttackCooldown = 1.2f;
    [Export] public int Damage = 1;
    [Export] public int HitFrame = 3;
    [Export] public float VerticalReach = 60f;   // ennél magasabban a játékos kikerüli az ütést
    [Export] public bool SpriteFacesRight = true;

    [Export] public string IdleAnim = "idle";
    [Export] public string WalkAnim = "walk";
    [Export] public string AttackAnim = "attack";

    private AnimatedSprite2D _sprite;
    private Node2D _player;
    private float _spriteBaseX;
    private float _cooldown;
    private bool _attacking;
    private bool _hitDone;
    private int _facing = 1;

    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _spriteBaseX = _sprite.Position.X;

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

        if (!IsOnFloor()) vel.Y += Gravity * dt;
        else vel.Y = 0;

        // halott vagy hiányzó játékos: a csoportból kikerül, ilyenkor nincs célpont
        if (_player == null || !IsInstanceValid(_player) || !_player.IsInGroup("player"))
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        _cooldown -= dt;

        if (_attacking)
        {
            // ütés közben nem mozog és nem fordul
            vel.X = 0;
        }
        else if (_player != null)
        {
            float dx = _player.GlobalPosition.X - GlobalPosition.X;

            if (Mathf.Abs(dx) > 5f)
            {
                _facing = dx > 0 ? 1 : -1;
                ApplyFacing();
            }

            if (Mathf.Abs(dx) > AttackRange)
            {
                vel.X = _facing * Speed;
                PlayAnim(WalkAnim);
            }
            else
            {
                vel.X = 0;
                if (_cooldown <= 0) StartAttack();
                else PlayAnim(IdleAnim);
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
        float s = _facing * (SpriteFacesRight ? 1f : -1f);
        _sprite.Scale = new Vector2(s, 1f);
        _sprite.Position = new Vector2(_spriteBaseX * s, _sprite.Position.Y);
    }

    private void StartAttack()
    {
        // ha nincs ütés animáció, ne ragadjon be az ütés állapotba
        if (_sprite.SpriteFrames == null || !_sprite.SpriteFrames.HasAnimation(AttackAnim))
        {
            DealDamage();
            _cooldown = AttackCooldown;
            return;
        }

        _attacking = true;
        _hitDone = false;
        _sprite.Play(AttackAnim);
    }

    private void OnFrameChanged()
    {
        if (_attacking && !_hitDone && _sprite.Animation == AttackAnim && _sprite.Frame >= HitFrame)
        {
            _hitDone = true;
            DealDamage();
        }
    }

    private void OnAnimationFinished()
    {
        if (!_attacking) return;

        // ha a HitFrame kimaradt (túl nagy érték), itt még sebez
        if (!_hitDone)
        {
            _hitDone = true;
            DealDamage();
        }

        _attacking = false;
        _cooldown = AttackCooldown;
    }

    private void DealDamage()
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        Vector2 d = _player.GlobalPosition - GlobalPosition;
        bool inFront = Mathf.Sign(d.X) == _facing;

        if (inFront
            && Mathf.Abs(d.X) <= AttackRange * 1.3f
            && Mathf.Abs(d.Y) <= VerticalReach
            && _player.HasMethod("TakeDamage"))
        {
            _player.Call("TakeDamage", Damage);
        }
    }

    private void PlayAnim(string name)
    {
        if (_sprite.SpriteFrames == null || !_sprite.SpriteFrames.HasAnimation(name)) return;
        if (_sprite.Animation != name || !_sprite.IsPlaying())
            _sprite.Play(name);
    }
}