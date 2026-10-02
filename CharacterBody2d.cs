using Godot;
 
public partial class CharacterBody2d : CharacterBody2D
{
public const float Speed = 300.0f;
public const float JumpVelocity = -400.0f;
 
public const float RollSpeed = 450.0f;
public const float RollDuration = 0.4f;
 
private float _rollTimer = 0.0f;
private bool _isRolling = false;
private float _rollDirection = 1.0f;
private bool _wasQPressed = false;
 
private AnimatedSprite2D _animatedSprite;
 
public override void _Ready()
{
_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
}
 
public override void _PhysicsProcess(double delta)
{
Vector2 velocity = Velocity;
 
if (!IsOnFloor())
{
velocity += GetGravity() * (float)delta;
}
 
// Ugrás
if ((Input.IsActionJustPressed("ui_accept")
|| Input.IsActionJustPressed("move_up"))
&& IsOnFloor())
{
velocity.Y = JumpVelocity;
}
 
// Bal / jobb mozgás
float direction = Input.GetAxis("move_left", "move_right");
 
if (!_isRolling)
{
if (direction != 0)
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
 
// Roll
bool isQCurrentlyPressed = Input.IsKeyPressed(Key.Q);
 
if (isQCurrentlyPressed
&& !_wasQPressed
&& IsOnFloor()
&& !_isRolling)
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
_rollTimer -= (float)delta;
 
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
}
