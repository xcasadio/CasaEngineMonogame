﻿using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Objects;

namespace CasaEngine.Framework.GameFramework;

/**
 * Controllers are non-physical actors that can possess a Pawn to control
 * its actions.  PlayerControllers are used by human players to control pawns, while
 * AIControllers implement the artificial intelligence for the pawns they control.
 * Controllers take control of a pawn using their Possess() method, and relinquish
 * control of the pawn by calling UnPossess().
 *
 * Controllers receive notifications for many of the events occurring for the Pawn they
 * are controlling.  This gives the controller the opportunity to implement the behavior
 * in response to this event, intercepting the event and superseding the Pawn's default
 * behavior.
 *
 * ControlRotation (accessed via GetControlRotation()), determines the viewing/aiming
 * direction of the controlled Pawn and is affected by input such as from a mouse or gamepad.
 *
 * @see https://docs.unrealengine.com/latest/INT/Gameplay/Framework/Controller/
 */
public class Controller : ObjectBase //Entity
{
    public Pawn Pawn { get; set; }
}