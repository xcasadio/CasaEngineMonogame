using System;
using System.Collections.Generic;
using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.RPGDemo.Components;
using CasaEngine.RPGDemo.Scripts;
using CasaEngine.RPGDemo.Weapons;
using Microsoft.Xna.Framework;
using SharpDX.XInput;

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
    public Vector3 Position => Owner.Coordinates.Position;
    public string AnimatationPrefix { get; set; }

    public bool CanAttack => _delayBeforeNewAttack <= 0.0f;


    public int Strength { get; set; } = 5;
    public int Defense { get; set; } = 3;
    public int HPMax { get; set; } = 10;
    public int HP { get; set; } = 10;


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
        _physics2dComponent = Owner.ComponentManager.GetComponent<Physics2dComponent>();
        AnimatedSpriteComponent = Owner.ComponentManager.GetComponent<AnimatedSpriteComponent>();
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
        }

        _physics2dComponent.Velocity = dir.ToVector3() * 120f;
        //_physics2dComponent.ApplyLinearImpulse(dir * 10f);
        //m_MovementDirection = dir_ * m_Spd * 10f;
    }

    private void MoveCharacter(float elapsedTime)
    {
        //m_MovementDirection = m_MovementDirection * elapsedTime_;
        //m_Body.ApplyLinearImpulse(ref m_MovementDirection);
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

    public void SetAnimation(AnimationIndices animationIndex)
    {
        var animationName = GetAnimationName(animationIndex);

        if (AnimatedSpriteComponent?.CurrentAnimation?.Animation2dData.AssetInfo.Name == animationName)
        {
            return;
        }

        LogManager.Instance.WriteLineTrace($"{Owner.Name} SetAnimation : {animationName}");
        AnimatedSpriteComponent.SetCurrentAnimation(animationName, true);
    }

    private string GetAnimationName(AnimationIndices animationIndex, string prefix = null)
    {
        return $"{(prefix == null ? AnimatationPrefix : prefix)}_{Enum.GetName(animationIndex)}_{Enum.GetName(CurrentDirection)}".ToLower();
    }

    private int GetAnimationDirectionOffset()
    {
        return _animationDirectionOffset[(int)CurrentDirection & _animationDirectionMask];
    }

    public void Hit(HitParameters hitParameters)
    {
        AddHitEffect(ref hitParameters.ContactPoint);

        var characterComponent = hitParameters.Entity.ComponentManager.GetComponent<CharacterComponent>();

        int cost = characterComponent.Character.Strength - Defense;
        cost = cost < 0 ? 0 : cost;
        HP -= cost;

        if (HP <= 0)
        {
            //characterComponent.Character.IKillSomeone(info_);

            //m_HP = m_HPMax;
            //SetPosition(Vector2.One * 10.0f);
            //characterComponent.Controller.StateMachine.Transition(Controller.GetState(0));
        }
    }

    public void AddHitEffect(ref Vector3 contactPoint)
    {

    }

    //public virtual void IKillSomeone(HitInfo info_)
    //{
    //    if (IsPLayer == true)
    //    {
    //        GameInfo.Instance.WorldInfo.BotKilled++;
    //    }
    //}

    //public bool CanAttackHim(ICollide2Dable other_)
    //{
    //    if (m_AlreadyAttacked.Contains(other_) == false)
    //    {
    //        return TeamInfo.CanAttack(other_.TeamInfo);
    //    }
    //
    //    return false;
    //}

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