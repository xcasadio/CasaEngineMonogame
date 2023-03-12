namespace EditorWpf.Datas
{
    public class DragAndDropInfo
    {
        public string Action { get; set; }
        public string Type { get; set; }
    }

    public class DragAndDropInfoAction
    {
        public const string Create = "Create";

    }

    public class DragAndDropInfoType
    {
        public const string Actor = "Actor";
    }
}
