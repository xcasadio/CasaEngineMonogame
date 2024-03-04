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
                    return args.Shift ? "�" : "�";
                case Keys.C:
                    return args.Shift ? "�" : "�";
                case Keys.E:
                    return args.Shift ? "�" : "�";
                case Keys.L:
                    return args.Shift ? "�" : "�";
                case Keys.N:
                    return args.Shift ? "�" : "�";
                case Keys.O:
                    return args.Shift ? "�" : "�";
                case Keys.S:
                    return args.Shift ? "�" : "�";
                case Keys.X:
                    return args.Shift ? "�" : "�";
                case Keys.Z:
                    return args.Shift ? "�" : "�";
            }
        }
        return base.KeyToString(args);
    }

}