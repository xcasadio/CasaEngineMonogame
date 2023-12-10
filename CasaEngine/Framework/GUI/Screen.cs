using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Control = TomShane.Neoforce.Controls.Control;

namespace CasaEngine.Framework.GUI
{
    public class Screen
    {
        public List<Control> Controls { get; } = new();
    }
}
