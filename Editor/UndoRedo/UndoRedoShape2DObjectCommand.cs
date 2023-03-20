using CasaEngine.Core.Maths.Shape2D;
using CasaEngine.Editor.UndoRedo;
using Editor.Game;

namespace Editor.UndoRedo
{
    public class UndoRedoShape2DObjectCommand
        : ICommand
    {

        Shape2dObject m_Object;





        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        public UndoRedoShape2DObjectCommand(Shape2dObject ob_)
        {
            if (ob_ == null)
            {
                throw new ArgumentNullException("UndoRedoShape2DObjectCommand() : ob_ is null");
            }

            m_Object = ob_.Clone();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg1_"></param>
        public void Execute(object arg1_)
        {
            if (arg1_ is Sprite2DEditorComponent)
            {
                Sprite2DEditorComponent c = arg1_ as Sprite2DEditorComponent;
                Shape2dObject temp = (Shape2dObject)c.CurrentShape2DManipulator.Shape2dObject.Clone();
                c.CurrentShape2DManipulator.Shape2dObject = m_Object;
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

    }
}
