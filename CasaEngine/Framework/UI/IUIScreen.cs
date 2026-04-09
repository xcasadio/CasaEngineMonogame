using MGUI.Core.UI;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.UI;

/// <summary>
/// A single logical UI screen (HUD, PauseMenu, InventoryScreen, etc.) that owns
/// one or more <see cref="MGWindow"/> instances and belongs to a specific <see cref="UILayer"/>.
/// </summary>
public interface IUIScreen
{
    /// <summary>The rendering/update layer this screen occupies.</summary>
    UILayer Layer { get; }

    /// <summary>
    /// When true this screen blocks input and updates from reaching any screen
    /// below it in the stack. Typically true for <see cref="UILayer.Modal"/> and
    /// <see cref="UILayer.Menu"/> screens.
    /// </summary>
    bool IsModal { get; }

    /// <summary>
    /// When true this screen blocks gameplay or view consumers below it at the engine level.
    /// Defaults to the same semantics as <see cref="IsModal"/>.
    /// </summary>
    bool BlocksViewsBelow { get; }

    /// <summary>Called once the first time the screen is pushed onto a <see cref="ScreenStack"/>.</summary>
    void Initialize(UIRoot root);

    /// <summary>Called when the screen becomes the topmost visible screen (or is pushed).</summary>
    void Show();

    /// <summary>Called when the screen is popped, removed, or the stack is cleared.</summary>
    void Hide();

    /// <summary>Called every frame while the screen is active (follows modal-blocking rules).</summary>
    void Update(GameTime gameTime);

    /// <summary>
    /// Returns all <see cref="MGWindow"/> instances owned by this screen.
    /// The <see cref="ScreenStack"/> registers them with the desktop on push
    /// and unregisters them on pop/remove.
    /// </summary>
    IEnumerable<MGWindow> GetWindows();
}
