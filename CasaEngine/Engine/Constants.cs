namespace CasaEngine.Engine
{
    public class Constants
    {
        public class FileNames
        {
            public static readonly string WorldExtension = ".world";

#if EDITOR
            public static readonly string MostRecentProjectsFileName = "mostRecentProjects.json";
#endif
        }
    }
}
