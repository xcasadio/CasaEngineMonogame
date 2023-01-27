using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.CoreSystems.Game;

namespace CasaEngine.FrontEnd.Screen
{
	/// <summary>
	/// Base class for screens that contain a menu of options. The user can
	/// move up and down to select an entry, or cancel to back out of the screen.
	/// </summary>
	public class MenuScreen 
		: Screen
	{
		#region Fields

		List<MenuEntry> menuEntries = new List<MenuEntry>();
		int selectedEntry = 0;
		string menuTitle;

		Renderer2DComponent m_Renderer2DComponent = null;

		#endregion

		#region Properties

		/// <summary>
		/// Gets the list of menu entries, so derived classes can add
		/// or change the menu contents.
		/// </summary>
		protected IList<MenuEntry> MenuEntries
		{
			get { return menuEntries; }
		}

		#endregion

		#region Initialization

		/// <summary>
		/// 
		/// </summary>
		/// <param name="menuTitle"></param>
		/// <param name="menuName_"></param>
		public MenuScreen(string menuTitle, string menuName_)
			: base(menuName_)
		{
			this.menuTitle = menuTitle;

			TransitionOnTime = TimeSpan.FromSeconds(0.5);
			TransitionOffTime = TimeSpan.FromSeconds(0.5);

            m_Renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);
		}

		#endregion

		#region Handle Input

		/// <summary>
		/// Responds to user input, changing the selected entry and accepting
		/// or cancelling the menu.
		/// </summary>
		public override void HandleInput(InputState input)
		{
			// Move to the previous menu entry?
			if (input.IsMenuUp(ControllingPlayer))
			{
				selectedEntry--;

				if (selectedEntry < 0)
					selectedEntry = menuEntries.Count - 1;
			}

			// Move to the next menu entry?
			if (input.IsMenuDown(ControllingPlayer))
			{
				selectedEntry++;

				if (selectedEntry >= menuEntries.Count)
					selectedEntry = 0;
			}

			// Accept or cancel the menu? We pass in our ControllingPlayer, which may
			// either be null (to accept input from any player) or a specific index.
			// If we pass a null controlling player, the InputState helper returns to
			// us which player actually provided the input. We pass that through to
			// OnSelectEntry and OnCancel, so they can tell which player triggered them.
			PlayerIndex playerIndex;

			if (input.IsMenuSelect(ControllingPlayer, out playerIndex))
			{
				OnSelectEntry(selectedEntry, playerIndex);
			}
			else if (input.IsMenuCancel(ControllingPlayer, out playerIndex))
			{
				OnCancel(playerIndex);
			}
		}

		/// <summary>
		/// Handler for when the user has chosen a menu entry.
		/// </summary>
		protected virtual void OnSelectEntry(int entryIndex, PlayerIndex playerIndex)
		{
			if (selectedEntry < 0 || selectedEntry >= menuEntries.Count)
			{
				return;
			}

			menuEntries[selectedEntry].OnSelectEntry(playerIndex);
		}

		/// <summary>
		/// Handler for when the user has cancelled the menu.
		/// </summary>
		protected virtual void OnCancel(PlayerIndex playerIndex)
		{
			ExitScreen();
		}

		/// <summary>
		/// Helper overload makes it easy to use OnCancel as a MenuEntry event handler.
		/// </summary>
		protected void OnCancel(object sender, PlayerIndexEventArgs e)
		{
			OnCancel(e.PlayerIndex);
		}
		
		#endregion

		#region Update and Draw

		/// <summary>
		/// Updates the menu.
		/// </summary>
		public override void Update(float elapsedTime, bool otherScreenHasFocus,
													   bool coveredByOtherScreen)
		{
			base.Update(elapsedTime, otherScreenHasFocus, coveredByOtherScreen);

			// Update each nested MenuEntry object.
			for (int i = 0; i < menuEntries.Count; i++)
			{
				bool isSelected = IsActive && (i == selectedEntry);

				menuEntries[i].Update(this, isSelected, elapsedTime);
			}
		}

		/// <summary>
		/// Draws the menu.
		/// </summary>
		public override void Draw(float elapsedTime_)
		{
			SpriteBatch spriteBatch = Engine.Instance.SpriteBatch;
			SpriteFont font = Engine.Instance.DefaultSpriteFont;

			Vector2 position = new Vector2(100, 150);

			// Make the menu slide into place during transitions, using a
			// power curve to make things look more interesting (this makes
			// the movement slow down as it nears the end).
			float transitionOffset = (float)System.Math.Pow(TransitionPosition, 2);

			if (ScreenState == ScreenState.TransitionOn)
				position.X -= transitionOffset * 256;
			else
				position.X += transitionOffset * 512;

			//spriteBatch.Begin();

			// Draw each menu entry in turn.
			for (int i = 0; i < menuEntries.Count; i++)
			{
				MenuEntry menuEntry = menuEntries[i];

				bool isSelected = IsActive && (i == selectedEntry);

				menuEntry.Draw(this, position, isSelected, elapsedTime_);

				position.Y += menuEntry.GetHeight(this);
			}

			// Draw the menu title.
			Vector2 titlePosition = new Vector2(426, 80);
			Vector2 titleOrigin = font.MeasureString(menuTitle) / 2.0f;
			titlePosition = Vector2.Subtract(titlePosition, titleOrigin);
			Color titleColor = new Color(192, 192, 192, TransitionAlpha);
			float titleScale = 1.25f;

			titlePosition.Y -= transitionOffset * 100;

			m_Renderer2DComponent.AddText2D(font, menuTitle, titlePosition, 0.0f, new Vector2(titleScale), titleColor, 0.99f);
			//spriteBatch.DrawString(font, menuTitle, titlePosition, titleColor, 0,
			//					   titleOrigin, titleScale, SpriteEffects.None, 0);

			//spriteBatch.End();
		}

		#endregion
	}
}
