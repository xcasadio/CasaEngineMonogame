﻿using System;

namespace CasaEngine.Framework.GameFramework;

/**
 * PlayerControllers are used by human players to control Pawns.
 *
 * ControlRotation (accessed via GetControlRotation()), determines the aiming
 * orientation of the controlled Pawn.
 *
 * In networked games, PlayerControllers exist on the server for every player-controlled pawn,
 * and also on the controlling client's machine. They do NOT exist on a client's
 * machine for pawns controlled by remote players elsewhere on the network.
 *
 * @see https://docs.unrealengine.com/latest/INT/Gameplay/Framework/Controller/PlayerController/
 */
public class PlayerController : Controller
{
    public Player Player { get; set; }
    public bool IsInputEnable { get; set; }
    //HUD

}