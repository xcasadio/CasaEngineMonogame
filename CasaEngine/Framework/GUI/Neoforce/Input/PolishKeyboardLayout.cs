namespace CasaEngine.Framework.GUI.Neoforce.Input;

public class PolishKeyboardLayout : KeyboardLayout
{

    public PolishKeyboardLayout()
    {
        Name = "Polish";
        LayoutList.Clear();
        LayoutList.Add(1045);
    }

    protected override string KeyToString(KeyEventArgs args)
    {
        if (args.Alt)
        {
            switch (args.Key)
            {
                case Keys.A:
                    return args.Shift ? "•" : "π";
                case Keys.C:
                    return args.Shift ? "∆" : "Ê";
                case Keys.E:
                    return args.Shift ? " " : "Í";
                case Keys.L:
                    return args.Shift ? "£" : "≥";
                case Keys.N:
                    return args.Shift ? "—" : "Ò";
                case Keys.O:
                    return args.Shift ? "”" : "Û";
                case Keys.S:
                    return args.Shift ? "å" : "ú";
                case Keys.X:
                    return args.Shift ? "è" : "ü";
                case Keys.Z:
                    return args.Shift ? "Ø" : "ø";
            }
        }
        return base.KeyToString(args);
    }

}