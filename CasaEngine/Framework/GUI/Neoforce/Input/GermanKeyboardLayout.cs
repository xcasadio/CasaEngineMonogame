namespace TomShane.Neoforce.Controls.Input;

public class GermanKeyboardLayout : KeyboardLayout
{

    public GermanKeyboardLayout()
    {
        Name = "German";
        LayoutList.Clear();
        LayoutList.Add(1031);
    }

    protected override string KeyToString(KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Keys.D0:
                return args.Shift ? "=" : "0";
            case Keys.D1:
                return args.Shift ? "!" : "1";
            case Keys.D2:
                return args.Shift ? "\"" : "2";
            case Keys.D3:
                return args.Shift ? "§" : "3";
            case Keys.D4:
                return args.Shift ? "$" : "4";
            case Keys.D5:
                return args.Shift ? "%" : "5";
            case Keys.D6:
                return args.Shift ? "&" : "6";
            case Keys.D7:
                return args.Shift ? "/" : "7";
            case Keys.D8:
                return args.Shift ? "(" : "8";
            case Keys.D9:
                return args.Shift ? ")" : "9";
            case Keys.OemBackslash:
                return args.Shift ? ">" : "<";
            case Keys.OemPlus:
                return args.Shift ? "*" : "+";
            case Keys.OemMinus:
                return args.Shift ? "_" : "-";
            case Keys.OemOpenBrackets:
                return args.Shift ? "?" : "ß";
            case Keys.OemCloseBrackets:
                return args.Shift ? "`" : "´";
            case Keys.OemQuestion:
                return args.Shift ? "'" : "#";
            case Keys.OemPeriod:
                return args.Shift ? ":" : ".";
            case Keys.OemComma:
                return args.Shift ? ";" : ",";
            case Keys.OemPipe:
                return args.Shift ? "°" : "^";
            case Keys.Space:
                return " ";
            case Keys.OemSemicolon:
                return args.Shift ? "Ü" : "ü";
            case Keys.OemQuotes:
                return args.Shift ? "Ä" : "ä";
            case Keys.OemTilde:
                return args.Shift ? "Ö" : "ö";

            case Keys.Decimal:
                return args.Shift ? "" : ".";

            default:
                return base.KeyToString(args);
        }
    }

}