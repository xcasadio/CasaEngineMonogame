namespace CasaEngine.Editor.UndoRedo
{
    public interface ICommand
    {
        void Execute(object arg1);

        void Undo(object arg1);
    }
}
