using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Editor.UndoRedo;
using Microsoft.Xna.Framework;
using Editor.Game;

namespace Editor.UndoRedo
{
    class UndoRedoHotSpotCommand
        : ICommand
    {
        #region Fields

        Point m_Object;

		#endregion

		#region Properties

		#endregion

		#region Constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ob_"></param>
        public UndoRedoHotSpotCommand(Point hotspot_)
		{
            m_Object = hotspot_;
		}

		#endregion

		#region ICommand Members

		/// <summary>
		/// 
		/// </summary>
		/// <param name="arg1_"></param>
        public void Execute(object arg1_)
        {
            if (arg1_ is Sprite2DEditorComponent)
            {
                Sprite2DEditorComponent c = arg1_ as Sprite2DEditorComponent;
                Point temp = c.CurrentSprite2D.HotSpot;
                c.CurrentSprite2D.HotSpot = m_Object; 
                m_Object = temp;
            }
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="arg1_"></param>
		public void Undo(object arg1_)
		{
            Execute(arg1_);
		}

		#endregion
    }
}
