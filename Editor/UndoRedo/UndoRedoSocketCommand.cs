using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Editor.UndoRedo;
using Microsoft.Xna.Framework;
using Editor.Game;

namespace Editor.UndoRedo
{
    class UndoRedoSocketCommand
        : ICommand
    {
        #region Fields

        KeyValuePair<string, Vector2> m_Object;
        int m_Index;

		#endregion

		#region Properties

		#endregion

		#region Constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pair_"></param>
		/// <param name="index_"></param>
        public UndoRedoSocketCommand(KeyValuePair<string, Vector2> pair_, int index_)
		{
            m_Object = pair_;
            m_Index = index_;
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
                KeyValuePair<string, Vector2> temp = c.CurrentSprite2D.GetSockets()[m_Index];
                c.CurrentSprite2D.ModifySocket(m_Object.Key, m_Object.Value);
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
