namespace CasaEngine.Framework.GameFramework;

/**
 *	Each player that is active on the current client has a LocalPlayer. It stays active across maps
 *	There may be several spawned in the case of splitscreen/coop.
 *	There may be 0 spawned on servers.
 */
public class LocalPlayer : Player
{
    //UGameViewportClient
    //int32 ControllerId = INVALID_CONTROLLERID;

    /** The platform user this player is assigned to, could correspond to multiple input devices */
    //FPlatformUserId PlatformUserId;
}