using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Editor.UndoRedo;
using Microsoft.Xna.Framework;
using Editor.Tools.Graphics2D;
using Editor.Game;

namespace Editor.UndoRedo
{
    class UndoRedoPolygoneCommand
        : ICommand
    {
        #region Fields

        Vector2 m_Point;
        int m_Index;
        bool m_IsAdd;

		#endregion

		#region Properties

		#endregion

		#region Constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="v_"></param>
		/// <param name="index_"></param>
		/// <param name="add_"></param>
        public UndoRedoPolygoneCommand(Vector2 v_, int index_, bool add_ = true)
		{
            m_Point = v_;
            m_Index = index_;
            m_IsAdd = add_;
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
                Shape2DManipulatorPolygone p = c.CurrentShape2DManipulator as Shape2DManipulatorPolygone;

                if (m_IsAdd == true)
                {
                    p.AddPoint(m_Point, m_Index);
                }
                else
                {
                    p.RemovePoint(m_Index);
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
                Shape2DManipulatorPolygone p = c.CurrentShape2DManipulator as Shape2DManipulatorPolygone;

                if (m_IsAdd == true)
                {                        
                    p.RemovePoint(m_Index);
                }
                else
                {
                    p.AddPoint(m_Point, m_Index);                        
                }
            }
		}

		#endregion
    }
}
