using CasaEngine.Core.Maths.Shape2D;
using CasaEngine.Editor.UndoRedo;
using Editor.Tools.Graphics2D;

namespace Editor.UndoRedo
{
    class UndoRedoAddCollisionCommand
        : ICommand
    {

        Shape2dObject m_Object;
        bool m_IsAdd;





        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        public UndoRedoAddCollisionCommand(Shape2dObject ob_, bool add_ = true)
        {
            if (ob_ == null)
            {
                throw new ArgumentNullException("UndoRedoAddCollisionCommand() : ob_ is null");
            }

            m_IsAdd = add_;

            m_Object = ob_.Clone();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg1_"></param>
        public void Execute(object arg1_)
        {
            if (arg1_ is Sprite2DEditorForm)
            {
                Sprite2DEditorForm form = arg1_ as Sprite2DEditorForm;

                if (m_IsAdd == true)
                {
                    form.AddCollision(m_Object, form.CurrentSprite2D);
                }
                else
                {
                    form.RemoveCollision(m_Object, form.CurrentSprite2D);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg1_"></param>
        public void Undo(object arg1_)
        {
            if (arg1_ is Sprite2DEditorForm)
            {
                Sprite2DEditorForm form = arg1_ as Sprite2DEditorForm;

                if (m_IsAdd == true)
                {
                    form.RemoveCollision(m_Object, form.CurrentSprite2D);
                }
                else
                {
                    form.AddCollision(m_Object, form.CurrentSprite2D);
                }
            }
        }

    }
}
