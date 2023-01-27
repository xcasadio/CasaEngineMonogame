using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Editor.UndoRedo;
using Microsoft.Xna.Framework;
using Editor.Game;

namespace Editor.UndoRedo
{
    class UndoRedoAddDeleteSocketCommand
        : ICommand
    {
        #region Fields

        string m_Name;
        Vector2 m_Position;
        bool m_Add;

		#endregion

		#region Properties

		#endregion

		#region Constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		/// <param name="pos_"></param>
        public UndoRedoAddDeleteSocketCommand(string name_, Vector2 pos_, bool add_)
		{
            m_Name = name_;
            m_Position = pos_;
            m_Add = add_;
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

                if (m_Add == true)
                {
                    c.CurrentSprite2D.AddSocket(m_Name, m_Position);
                }
                else
                {
                    c.CurrentSprite2D.RemoveSocket(m_Name);
                }
            }
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="arg1_"></param>
		public void Undo(object arg1_)
		{
            if (arg1_ is Sprite2DEditorComponent)
            {
                Sprite2DEditorComponent c = arg1_ as Sprite2DEditorComponent;

                if (m_Add == true)
                {
                    c.CurrentSprite2D.RemoveSocket(m_Name);
                }
                else
                {
                    c.CurrentSprite2D.AddSocket(m_Name, m_Position);
                }
            }
		}

		#endregion
    }
}
