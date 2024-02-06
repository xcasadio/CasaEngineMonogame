using System;
using System.Collections.Generic;
using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Log;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.RPGDemo.Scripts;
using CasaEngine.RPGDemo.Weapons;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Controllers;

public class Character
{
    public const float DeadZone = 0.2f;

    public enum AnimationIndices
    {
        Stand = 0,
        Walk = 1,
        Attack = 2,
        Attack2 = 3,
        Attack3 = 4,
        Hit = 5,
        Magic1 = 6,
        Magic2 = 7,
        Dying = 8,
        Dead = 9,
        ToRageState = 10,
        ToNormalState = 11
    }

    public enum AnimationDirectionOffset
    {
        Right = 0,
        Left = 1,
        Up = 2,
        Down = 3,
        UpRight = 4,
        DownRight = 5,
        UpLeft = 6,
        DownLeft = 7,
    }

    private int _numberOfDirection = 4;
    private int _animationDirectionMask = 0;
    private readonly Dictionary<int, int> _animationDirectionOffset = new();
    private Weapon _weapon;
    private float _delayBeforeNewAttack;

    private Physics2dComponent _physics2dComponent;

    public AnimatedSpriteComponent AnimatedSpriteComponent { get; private set; }

    public Character2dDirection CurrentDirection { get; set; } = Character2dDirection.Right;
    public Entity Owner { get; }
    public CharacterType Type { get; set; }
    public int ComboNumber { get; set; }
    public Vector3 Position => Owner.RootComponent?.Position ?? Vector3.Zero;
    public string AnimatationPrefix { get; set; }

    public bool CanAttack => _delayBeforeNewAttack <= 0.0f;
    public bool IsDead => HP <= 0;


    public int Strength { get; set; } = 5;
    public int Defense { get; set; } = 3;
    public int HPMax { get; set; } = 10;
    public int HP { get; set; } = 10;
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public int ExperienceBeforeNextLevel { get; set; } = 10;


    public Character(Entity entity)
    {
        Owner = entity;

        SetAnimationDirectionOffset(Character2dDirection.Down, (int)AnimationDirectionOffset.Down);
        //SetAnimationDirectionOffset(Character2dDirection.DownLeft, (int)AnimationDirectionOffset.DownLeft);
        //SetAnimationDirectionOffset(Character2dDirection.DownRight, (int)AnimationDirectionOffset.DownRight);
        SetAnimationDirectionOffset(Character2dDirection.Right, (int)AnimationDirectionOffset.Right);
        SetAnimationDirectionOffset(Character2dDirection.Left, (int)AnimationDirectionOffset.Left);
        SetAnimationDirectionOffset(Character2dDirection.Up, (int)AnimationDirectionOffset.Up);
        //SetAnimationDirectionOffset(Character2dDirection.UpLeft, (int)AnimationDirectionOffset.UpLeft);
        //SetAnimationDirectionOffset(Character2dDirection.UpRight, (int)AnimationDirectionOffset.UpRight);
        SetAnimationParameters(4, -1);
    }

    public void Initialize(CasaEngineGame game)
    {
        _physics2dComponent = Owner.GetComponent<Physics2dComponent>();
        AnimatedSpriteComponent = Owner.GetComponent<AnimatedSpriteComponent>();
    }

    public void Update(float elapsedTime)
    {
        _delayBeforeNewAttack -= elapsedTime;

        MoveCharacter(elapsedTime);
    }

    public void Load(JsonElement element)
    {
        //XmlElement statusNode = (XmlElement)el.SelectSingleNode("Status");
    }


    //public override void IKillSomeone(HitInfo info)
    //{
    //    base.IKillSomeone(info);
    //
    //    Character c = (Character)info.ActorHit;
    //
    //    if (c.IsPLayer == false)
    //    {
    //        GameInfo.Instance.WorldInfo.Score += c.Score;
    //    }
    //}

    public void SetPosition(Vector2 pos)
    {
        //m_Body.Position = pos_;
    }

    public void Move(ref Vector2 dir)
    {
        if (dir == Vector2.Zero)
        {
            //always when Vector2.Zero to stop movement
            //else if contact the character will continue to move
            _physics2dComponent.Velocity = dir.ToVector3();
            return;
        }

        var maxVelocity = 120f;
        _physics2dComponent.Velocity = dir.ToVector3() * maxVelocity;
        //_physics2dComponent.ApplyImpulse(dir.ToVector3() * maxVelocity, Vector3.Zero);
        //
        //if (_physics2dComponent.Velocity.Length() > 120.0f)
        //{
        //    var velocity = _physics2dComponent.Velocity;
        //    velocity.Normalize();
        //    _physics2dComponent.Velocity = velocity * maxVelocity;
        //}

        //m_MovementDirection = dir_ * m_Spd * 10f;
    }

    private void MoveCharacter(float elapsedTime)
    {
        //m_MovementDirection = m_MovementDirection * elapsedTime_;
        //_physics2dComponent.ApplyImpulse(ref m_MovementDirection);
    }

    public void SetAnimationParameters(int numberOfDirectionAnimation, int animationDirectionMask)
    {
        _numberOfDirection = numberOfDirectionAnimation;
        _animationDirectionMask = animationDirectionMask;
    }

    public void SetAnimationDirectionOffset(Character2dDirection dir, int offset)
    {
        _animationDirectionOffset[(int)dir] = offset;
    }

    /// <summary>
    /// Set the animation compared to the index and the direction of the character
    /// See AnimationDirectionMask, CharacterDirection, AnimationIndices, NumberCharacterDriection
    /// </summary>
    /// <param name="index"></param>
    //public void SetAnimation(int index)
    //{
    //    _animatedSpriteComponent.SetCurrentAnimation(index * _numberOfDirection + GetAnimationDirectionOffset(), true);
    //}
    //public void SetAnimation(string animationName)
    //{
    //    _animatedSpriteComponent.SetCurrentAnimation(animationName, true);
    //}

    public void SetAnimation(AnimationIndices animationIndex, bool withDirection = true)
    {
        var animationName = GetAnimationName(animationIndex, null, withDirection);

        if (AnimatedSpriteComponent?.CurrentAnimation?.Animation2dData.Name == animationName)
        {
            return;
        }

        Logs.WriteTrace($"{Owner.Name} SetAnimation : {animationName}");
        AnimatedSpriteComponent.SetCurrentAnimation(animationName, true);
    }

    private string GetAnimationName(AnimationIndices animationIndex, string prefix = null, bool withDirection = true)
    {
        string format = "{0}_{1}_{2}";
        if (!withDirection)
        {
            format = "{0}_{1}";
        }

        return string.Format(format,
            prefix == null ? AnimatationPrefix : prefix,
            Enum.GetName(animationIndex),
            Enum.GetName(CurrentDirection))
            .ToLower();
    }

    private int GetAnimationDirectionOffset()
    {
        return _animationDirectionOffset[(int)CurrentDirection & _animationDirectionMask];
    }

    public void Hit(HitParameters hitParameters)
    {
        AddHitEffect(ref hitParameters.ContactPoint);

        var opponentGamePlayComponent = hitParameters.Entity;

        if (opponentGamePlayComponent != null)
        {
            if (opponentGamePlayComponent.GameplayProxy is IScriptCharacter opponentScriptCharacter
                && !IsDead)
            {
                var physics2dComponent = opponentScriptCharacter.Character.Owner.GetComponent<Physics2dComponent>();
                var impulse = Vector3.UnitX * 300f; //Vector3.Normalize(hitParameters.Entity.Coordinates.WorldMatrix.Forward) * -100f;
                physics2dComponent.ApplyImpulse(impulse, Vector3.Zero);

                int attackValue = opponentScriptCharacter.Character.Strength - Defense;
                attackValue = attackValue < 0 ? 0 : attackValue;
                HP = Math.Max(HP - attackValue, 0);

                if (HP == 0)
                {
                    opponentScriptCharacter.Character.KillSomeone(Experience);
                    //var characterComponent = Owner.ComponentManager.GetComponent<CharacterComponent>();
                    //characterComponent.Controller.StateMachine.Transition(characterComponent.Controller.GetState(dying));
                    //Owner.Destroy();
                    if (Owner.GameplayProxy is IScriptCharacter scriptCharacter)
                    {
                        scriptCharacter.Dying();
                    }

                    //m_HP = m_HPMax;
                    //SetPosition(Vector2.One * 10.0f);
                    //characterComponent.Controller.StateMachine.Transition(Controller.GetState(0));
                }
                else
                {
                    opponentScriptCharacter.Controller.StateMachine.HandleMessage(
                        new Message(Owner.Id, opponentGamePlayComponent.Id, (int)MessageType.Hit, 0, null));
                }
            }
        }
    }

    private void KillSomeone(int experience)
    {
        Experience += experience;

        if (Experience > ExperienceBeforeNextLevel)
        {
            Experience -= ExperienceBeforeNextLevel;
            Level++;
        }
    }

    public void AddHitEffect(ref Vector3 contactPoint)
    {

    }

    //public bool CanAttackHim(ICollide2Dable other_)
    //{
    //    if (m_AlreadyAttacked.Contains(other_) == false)
    //    {
    //        return TeamInfo.CanAttack(other_.TeamInfo);
    //    }
    //
    //    return false;
    //}

    public void Dying()
    {
        Owner.Destroy();

        //var entity = _game.GameManager.SpawnEntity("smoke_ring_effect");
        //entity.Coordinates.LocalPosition = Owner.Coordinates.LocalPosition;
        //entity.Initialize(_game);
        //var animatedSpriteComponent = entity.ComponentManager.GetComponent<AnimatedSpriteComponent>();
        //animatedSpriteComponent.SetCurrentAnimation(0, true);
    }

    public void DoANewAttack()
    {
        _delayBeforeNewAttack = 1.0f;
        //m_AlreadyAttacked.Clear();
    }

    public void SetWeapon(Weapon weapon)
    {
        _weapon = weapon;
        _weapon.Character = this;
    }

    public void AttachWeapon()
    {
        _weapon.Attach();
    }

    public void UnAttachWeapon()
    {
        _weapon.UnAttachWeapon();
    }

    public bool HandleMessage(Message message)
    {
        //switch (message.Type)
        //{
        //    case MessageType.Hit:
        //        Hit((HitInfo)message.ExtraInfo);
        //        break;
        //    case MessageType.IHitSomeone:
        //        HitInfo hitInfo = (HitInfo)message.ExtraInfo;
        //        m_AlreadyAttacked.Add(hitInfo.ActorHit as ICollide2Dable);
        //        break;
        //}
        //_fsm.HandleMessage(message);
        //
        //if (m_Controller != null)
        //{
        //    m_Controller.StateMachine.HandleMessage(message);
        //}

        return true;
    }

    public static Character2dDirection GetCharacterDirectionFromVector2(Vector2 v)
    {
        Character2dDirection dir = 0;

        if (v.X < -DeadZone)
        {
            dir = Character2dDirection.Left;
        }
        else if (v.X > DeadZone)
        {
            dir = Character2dDirection.Right;
        }

        if (v.Y < -DeadZone)
        {
            dir = Character2dDirection.Down;
        }
        else if (v.Y > DeadZone)
        {
            dir = Character2dDirection.Up;
        }

        return dir;
    }
}