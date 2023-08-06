using System;
using System.Collections.Generic;
using System.Text.Json;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers;

public class Character
{
    public enum AnimationIndices
    {
        Idle = 0,
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

    private Physics2dComponent _physics2dComponent;
    private AnimatedSpriteComponent _animatedSpriteComponent;

    public Character2dDirection CurrentDirection { get; set; } = Character2dDirection.Right;
    public Entity Owner { get; }
    public CharacterType Type { get; set; }
    public int ComboNumber { get; set; }
    public Vector3 Position => Owner.Coordinates.Position;

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
        _animatedSpriteComponent = Owner.ComponentManager.GetComponent<AnimatedSpriteComponent>();
    }

    public void Update(float elapsedTime)
    {
        throw new NotImplementedException();
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
            //m_Body.ResetDynamics();
        }

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
        var prefix = "swordman";
        var animationName = $"{prefix}_{Enum.GetName(animationIndex)}_{Enum.GetName(CurrentDirection)}".ToLower();
        _animatedSpriteComponent.SetCurrentAnimation(animationName, true);
    }

    private int GetAnimationDirectionOffset()
    {
        return _animationDirectionOffset[(int)CurrentDirection & _animationDirectionMask];
    }

    //public void Hit(HitInfo info_)
    //{
    //    CharacterActor2D a = (CharacterActor2D)info_.ActorAttacking;
    //
    //    AddHitEffect(ref info_.ContactPoint);
    //
    //    int cost = a.Strength - Defense;
    //    cost = cost < 0 ? 0 : cost;
    //    HP -= cost;
    //
    //    if (HP <= 0)
    //    {
    //        a.IKillSomeone(info_);
    //        Delete = true;
    //
    //        //to delete : debug
    //        //respawn
    //        m_HP = m_HPMax;
    //        SetPosition(Vector2.One * 10.0f);
    //        m_Controller.StateMachine.Transition(m_Controller.GetState(0));
    //    }
    //}

    public void AddHitEffect(ref Vector2 contactPoint)
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
        //m_AlreadyAttacked.Clear();
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
        float deadzone = 0.2f;
        Character2dDirection dir = 0;

        if (v.X < -deadzone)
        {
            dir |= Character2dDirection.Left;
        }
        else if (v.X > deadzone)
        {
            dir |= Character2dDirection.Right;
        }

        if (v.Y < -deadzone)
        {
            dir |= Character2dDirection.Up;
        }
        else if (v.Y > deadzone)
        {
            dir |= Character2dDirection.Down;
        }

        return dir;
    }
}