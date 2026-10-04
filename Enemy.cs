using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public int MaxHp = 1;                // 1 = egy találattól eltűnik
    [Export] public float Speed = 80f;
    [Export] public float Gravity = 1200f;
    [Export] public float AttackRange = 50f;
    [Export] public float AttackCooldown = 1.2f;
    [Export] public int Damage = 1;
    [Export] public int HitFrame = 3;             // az ütés melyik képkockánál sebez
    [Export] public int AttackEndFrame = -1;      // -1 = az utolsó képkockánál ér véget az ütés
    [Export] public float AttackMaxTime = 1.5f;   // biztonsági határ (lassításnál automatikusan nő)
    [Export] public float AttackSpeedScale = 0.6f; // ütés animáció sebessége (1.0 = eredeti)
    [Export] public float AttackLinger = 0.15f;   // ennyi ideig marad az utolsó képkocka a végén
    [Export] public float VerticalReach = 60f;
    [Export] public bool SpriteFacesRight = true;

    [Export] public string IdleAnim = "default";
    [Export] public string WalkAnim = "default";
    [Export] public string AttackAnim = "attack";

    private AnimatedSprite2D _sprite;
    private Node2D _player;
    private float _spriteBaseX;
    private float _cooldown;
    private float _attackTimer;
    private float _lingerTimer;
    private bool _attacking;
    private bool _attackAnimDone;   // az ütés animáció elérte a végét, az utolsó képkocka még látszik
    private bool _hitDone;
    private bool _dead;
    private int _hp;
    private int _facing = 1; // 1 = jobbra, -1 = balra

    public override void _Ready()
    {
        _hp = MaxHp;
        AddToGroup("enemy");

        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _spriteBaseX = _sprite.Position.X;

        // az ütés animáció ne ismétlődjön
        if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(AttackAnim))
            _sprite.SpriteFrames.SetAnimationLoop(AttackAnim, false);

        _sprite.AnimationFinished += OnAnimationFinished;
        _sprite.FrameChanged += OnFrameChanged;

        PlayAnim(IdleAnim);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead) return;

        float dt = (float)delta;
        Vector2 vel = Velocity;

        // gravitáció
        if (!IsOnFloor()) vel.Y += Gravity * dt;
        else vel.Y = 0;

        // játékos keresése (ha meghalt, kikerül a csoportból)
        if (_player == null || !IsInstanceValid(_player) || !_player.IsInGroup("player"))
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        _cooldown -= dt;

        if (_attacking)
        {
            if (_attackAnimDone)
            {
                // az animáció véget ért, az utolsó képkockát még egy kicsit látni engedjük
                _lingerTimer -= dt;
                if (_lingerTimer <= 0)
                    EndAttack();
            }
            else
            {
                // biztonsági időzítő: ha a jel nem jönne, akkor is vége az ütésnek
                _attackTimer -= dt;
                if (_attackTimer <= 0)
                    EndAttack();
            }
        }

        if (_attacking)
        {
            // ütés közben nem mozog és nem fordul
            vel.X = 0;
        }
        else if (_player != null)
        {
            float dx = _player.GlobalPosition.X - GlobalPosition.X;

            // fordulás a játékos felé
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
        // tükrözés scale-lel, az X eltolást is tükrözzük
        float s = _facing * (SpriteFacesRight ? 1f : -1f);
        _sprite.Scale = new Vector2(s, 1f);
        _sprite.Position = new Vector2(_spriteBaseX * s, _sprite.Position.Y);
    }

    // ---------- Sebződés és eltűnés ----------
    public void TakeDamage(int damage)
    {
        if (_dead) return;

        _hp -= damage;

        if (_hp <= 0)
        {
            Die();
            return;
        }

        // piros villanás
        _sprite.Modulate = new Color(1f, 0.35f, 0.35f);
        CreateTween().TweenProperty(_sprite, "modulate", Colors.White, 0.25f);
    }

    private void Die()
    {
        _dead = true;
        _attacking = false;
        _sprite.SpeedScale = 1f;
        RemoveFromGroup("enemy");

        // ne ütközzön és ne sebezzen tovább
        GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.SetDeferred("disabled", true);
        Velocity = Vector2.Zero;

        // gyors elhalványulás, aztán törlés
        var tween = CreateTween();
        tween.TweenProperty(_sprite, "modulate:a", 0.0f, 0.3f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    // ---------- Támadás ----------
    private void StartAttack()
    {
        // ha nincs ütés animáció, ne ragadjon be az ütés állapotba
        if (_sprite.SpriteFrames == null || !_sprite.SpriteFrames.HasAnimation(AttackAnim))
        {
            DealDamage();
            _cooldown = AttackCooldown;
            return;
        }

        float speedScale = Mathf.Max(0.05f, AttackSpeedScale);

        _attacking = true;
        _attackAnimDone = false;
        _hitDone = false;
        _attackTimer = AttackMaxTime / speedScale;
        _lingerTimer = AttackLinger;

        _sprite.SpeedScale = speedScale;
        _sprite.Play(AttackAnim);
    }

    private void OnFrameChanged()
    {
        if (_dead || !_attacking || _sprite.Animation != AttackAnim) return;

        if (!_hitDone && _sprite.Frame >= HitFrame)
        {
            _hitDone = true;
            DealDamage();
        }

        int lastFrame = _sprite.SpriteFrames.GetFrameCount(AttackAnim) - 1;
        int endFrame = AttackEndFrame < 0 ? lastFrame : Mathf.Min(AttackEndFrame, lastFrame);

        // az utolsó (vagy megadott) képkockánál az animáció "véget ért", de még látszik
        if (_sprite.Frame >= endFrame && !_attackAnimDone)
        {
            _attackAnimDone = true;
            _lingerTimer = AttackLinger;
        }
    }

    private void OnAnimationFinished()
    {
        // tartalék: ha a fenti még nem jelezte a végét
        if (_attacking && !_attackAnimDone)
        {
            _attackAnimDone = true;
            _lingerTimer = AttackLinger;
        }
    }

    private void EndAttack()
    {
        if (!_attacking) return;

        // ha a sebzés képkockája kimaradt, itt még sebez
        if (!_hitDone)
        {
            _hitDone = true;
            DealDamage();
        }

        _attacking = false;
        _attackAnimDone = false;
        _cooldown = AttackCooldown;

        _sprite.SpeedScale = 1f;   // a séta és az idle normál sebességgel megy
        PlayAnim(IdleAnim);        // vissza az alapállapotba
    }

    private void DealDamage()
    {
        if (_dead || _player == null || !IsInstanceValid(_player)) return;

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
        if (_sprite.SpriteFrames == null || !_sprite.SpriteFrames.HasAnimation(name))
        {
            GD.PrintErr("Enemy: nincs ilyen animáció: ", name);
            return;
        }
        if (_sprite.Animation != name || !_sprite.IsPlaying())
            _sprite.Play(name);
    }
}