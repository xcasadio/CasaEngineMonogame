using System.Collections.Generic;

namespace CasaEngine.Framework.Input;

public interface IWindowFileDropSource
{
    void DrainDroppedFiles(ICollection<string> filePaths);
}