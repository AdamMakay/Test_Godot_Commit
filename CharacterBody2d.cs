using Godot;

public partial class CharacterBody2d : CharacterBody2D
{
	public const float Speed = 300.0f;
	public const float JumpVelocity = -400.0f;

	public const float RollSpeed = 450.0f;
	public const float RollDuration = 0.4f;

	// ---------- HP ----------
	[Export] public int MaxHp = 5;
	[Export] public float InvincibleTime = 0.8f;   // sebzés utáni védettség (mp)
	[Export] public bool RollInvincible = true;    // gurulás közben nem sebezhető
	[Export] public string DeathAnim = "death";

	// ---------- Ütés ---------- // ÚJ
	[Export] public string AttackAnim = "attack";
	[Export] public int AttackDamage = 1;
	[Export] public float AttackRange = 90f;        // vízszintes hatótáv
	[Export] public float AttackVerticalReach = 70f; // függőleges tűrés
	[Export] public int AttackHitFrame = 2;         // melyik képkockánál sebez
	[Export] public int AttackEndFrame = -1;        // -1 = az utolsó képkockánál ér véget
	[Export] public float AttackMaxTime = 0.8f;     // biztonsági határ
	[Export] public float AttackCooldown = 0.25f;   // két ütés között

	public int Hp { get; private set; }
	private bool _invincible;
	private bool _dead;
	private ProgressBar _hpBar;

	private bool _isAttacking;          // ÚJ
	private bool _attackHitDone;        // ÚJ
	private float _attackTimer;         // ÚJ
	private float _attackCooldownTimer; // ÚJ

	private float _rollTimer = 0.0f;
	private bool _isRolling = false;
	private float _rollDirection = 1.0f;
	private bool _wasQPressed = false;

	private AnimatedSprite2D _animatedSprite;
	
	private Label _timerLabel;
	private float _survivalTime = 0f;

	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

		// ÚJ: az ütés animáció ne ismétlődjön, és figyeljük a képkockákat
		if (_animatedSprite.SpriteFrames != null && _animatedSprite.SpriteFrames.HasAnimation(AttackAnim))
			_animatedSprite.SpriteFrames.SetAnimationLoop(AttackAnim, false);

		_animatedSprite.FrameChanged += OnFrameChanged;
		_animatedSprite.AnimationFinished += OnAnimationFinished;

		SetupHp();
		_timerLabel = GetNode<Label>("../TileMap/TimerLabel");
		_timerLabel.Text = "00:00";
		_timerLabel.Visible = true;
		_timerLabel.Scale = new Vector2(2, 2);
		_timerLabel.Modulate = Colors.White;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_survivalTime += dt;

		int minutes = (int)(_survivalTime / 60);
		int seconds = (int)(_survivalTime % 60);

		if (_timerLabel != null)
		{
			_timerLabel.Text = $"{minutes:00}:{seconds:00}";
		}
		
		Vector2 velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity += GetGravity() * dt;
		}

		// Halál: nincs input, csak a gravitáció működik
		if (_dead)
		{
			velocity.X = 0;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		// ÚJ: ütés indítása és időzítők
		_attackCooldownTimer -= dt;

		if (Input.IsActionJustPressed("attack")
			&& !_isAttacking
			&& !_isRolling
			&& _attackCooldownTimer <= 0)
		{
			StartAttack();
		}

		if (_isAttacking)
		{
			_attackTimer -= dt;
			if (_attackTimer <= 0)
				EndAttack();
		}

		// Ugrás (ütés közben nem lehet)
		if ((Input.IsActionJustPressed("ui_accept")
			|| Input.IsActionJustPressed("move_up"))
			&& IsOnFloor()
			&& !_isAttacking)
		{
			velocity.Y = JumpVelocity;
		}

		// Bal / jobb mozgás
		float direction = Input.GetAxis("move_left", "move_right");

		if (!_isRolling)
		{
			if (_isAttacking && IsOnFloor())
			{
				// ÚJ: földön ütés közben megáll
				velocity.X = 0;
			}
			else if (direction != 0)
			{
				velocity.X = direction * Speed;
			}
			else
			{
				velocity.X = Mathf.MoveToward(
					velocity.X,
					0,
					Speed
				);
			}
		}

		// Roll (ütés közben nem lehet)
		bool isQCurrentlyPressed = Input.IsKeyPressed(Key.Q);

		if (isQCurrentlyPressed
			&& !_wasQPressed
			&& IsOnFloor()
			&& !_isRolling
			&& !_isAttacking)
		{
			_isRolling = true;
			_rollTimer = RollDuration;

			_rollDirection =
				direction != 0
					? Mathf.Sign(direction)
					: (_animatedSprite.FlipH ? -1f : 1f);
		}

		_wasQPressed = isQCurrentlyPressed;

		if (_isRolling)
		{
			_rollTimer -= dt;

			if (_rollTimer <= 0)
			{
				_isRolling = false;
			}
			else
			{
				velocity.X = _rollDirection * RollSpeed;
			}
		}

		Velocity = velocity;
		MoveAndSlide();

		Animate(direction);
	}

	private void Animate(float direction)
	{
		// ÚJ: ütés közben nem fordul meg és nem vált animációt
		if (_isAttacking)
			return;

		// Fordulás földön és levegőben is
		if (direction != 0)
		{
			_animatedSprite.FlipH = direction < 0;
		}

		if (_isRolling)
		{
			if (_animatedSprite.Animation != "roll")
				_animatedSprite.Play("roll");

			return;
		}

		if (!IsOnFloor())
		{
			if (_animatedSprite.Animation != "jump")
				_animatedSprite.Play("jump");

			return;
		}

		if (Mathf.Abs(Velocity.X) > 10)
		{
			if (_animatedSprite.Animation != "run")
				_animatedSprite.Play("run");

			return;
		}

		if (_animatedSprite.Animation != "idle")
			_animatedSprite.Play("idle");
	}

	// ---------- Ütés ---------- // ÚJ
	private void StartAttack()
	{
		if (_animatedSprite.SpriteFrames == null || !_animatedSprite.SpriteFrames.HasAnimation(AttackAnim))
		{
			GD.PrintErr("Player: nincs ilyen animáció: ", AttackAnim);
			return;
		}

		_isAttacking = true;
		_attackHitDone = false;
		_attackTimer = AttackMaxTime;
		_animatedSprite.Play(AttackAnim);
	}

	private void OnFrameChanged()
	{
		if (!_isAttacking || _animatedSprite.Animation != AttackAnim) return;

		if (!_attackHitDone && _animatedSprite.Frame >= AttackHitFrame)
		{
			_attackHitDone = true;
			HitEnemies();
		}

		int lastFrame = _animatedSprite.SpriteFrames.GetFrameCount(AttackAnim) - 1;
		int endFrame = AttackEndFrame < 0 ? lastFrame : Mathf.Min(AttackEndFrame, lastFrame);

		if (_animatedSprite.Frame >= endFrame)
			EndAttack();
	}

	private void OnAnimationFinished()
	{
		if (_isAttacking)
			EndAttack();
	}

	private void EndAttack()
	{
		if (!_isAttacking) return;

		if (!_attackHitDone)
		{
			_attackHitDone = true;
			HitEnemies();
		}

		_isAttacking = false;
		_attackCooldownTimer = AttackCooldown;
	}

	private void HitEnemies()
	{
		int facing = _animatedSprite.FlipH ? -1 : 1;

		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(enemy)) continue;

			Vector2 d = enemy.GlobalPosition - GlobalPosition;

			bool inFront = d.X * facing >= -10f;   // kis tűrés a háta mögött is
			if (inFront
				&& Mathf.Abs(d.X) <= AttackRange
				&& Mathf.Abs(d.Y) <= AttackVerticalReach
				&& enemy.HasMethod("TakeDamage"))
			{
				enemy.Call("TakeDamage", AttackDamage);
			}
		}
	}

	// ---------- HP rendszer ----------
	private void SetupHp()
	{
		Hp = MaxHp;
		AddToGroup("player");

		// HP csík a bal felső sarokban, kódból létrehozva
		var layer = new CanvasLayer();
		_hpBar = new ProgressBar
		{
			MaxValue = MaxHp,
			Value = Hp,
			ShowPercentage = false,
			Position = new Vector2(20, 20),
			CustomMinimumSize = new Vector2(200, 20)
		};
		layer.AddChild(_hpBar);
		AddChild(layer);
	}

	public void TakeDamage(int damage)
	{
		if (_dead || _invincible) return;
		if (RollInvincible && _isRolling) return;

		Hp = Mathf.Max(0, Hp - damage);
		_hpBar.Value = Hp;

		if (Hp == 0)
		{
			Die();
			return;
		}

		_invincible = true;
		GetTree().CreateTimer(InvincibleTime).Timeout += () => _invincible = false;

		// piros villanás
		_animatedSprite.Modulate = new Color(1f, 0.35f, 0.35f);
		CreateTween().TweenProperty(_animatedSprite, "modulate", Colors.White, InvincibleTime);
	}

	private void Die()
	{
		_dead = true;
		GD.Print($"Survived {_survivalTime:F1} seconds");
		_isRolling = false;
		_isAttacking = false;   // ÚJ
		RemoveFromGroup("player");   // az ellenségek innentől nem támadják

		if (_animatedSprite.SpriteFrames != null && _animatedSprite.SpriteFrames.HasAnimation(DeathAnim))
			_animatedSprite.Play(DeathAnim);

		// 2 mp után újraindul a pálya
		GetTree().CreateTimer(2.0).Timeout += () => GetTree().ReloadCurrentScene();
	}
}
