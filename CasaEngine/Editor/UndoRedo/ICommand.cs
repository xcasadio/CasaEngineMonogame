namespace CasaEngine.Editor.UndoRedo
{
    public interface ICommand
    {
        void Execute(object arg1_);

        void Undo(object arg1_);
    }
}
