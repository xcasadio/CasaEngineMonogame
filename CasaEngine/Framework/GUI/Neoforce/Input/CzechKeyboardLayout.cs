namespace TomShane.Neoforce.Controls.Input;

public class CzechKeyboardLayout : KeyboardLayout
{

    public CzechKeyboardLayout()
    {
        Name = "Czech";
        LayoutList.Clear();
        LayoutList.Add(1029);
    }

    protected override string KeyToString(KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Keys.D0:
                return args.Shift ? "0" : "é";
            case Keys.D1:
                return args.Shift ? "1" : "+";
            case Keys.D2:
                return args.Shift ? "2" : "ì";
            case Keys.D3:
                return args.Shift ? "3" : "š";
            case Keys.D4:
                return args.Shift ? "4" : "è";
            case Keys.D5:
                return args.Shift ? "5" : "ø";
            case Keys.D6:
                return args.Shift ? "6" : "ž";
            case Keys.D7:
                return args.Shift ? "7" : "ý";
            case Keys.D8:
                return args.Shift ? "8" : "á";
            case Keys.D9:
                return args.Shift ? "9" : "í";

            case Keys.OemPlus:
                return args.Shift ? "¡" : "´";
            case Keys.OemMinus:
                return args.Shift ? "%" : "=";
            case Keys.OemOpenBrackets:
                return args.Shift ? "/" : "ú";
            case Keys.OemCloseBrackets:
                return args.Shift ? "(" : ")";
            case Keys.OemQuestion:
                return args.Shift ? "_" : "-";
            case Keys.OemPeriod:
                return args.Shift ? ":" : ".";
            case Keys.OemComma:
                return args.Shift ? "?" : ",";
            case Keys.OemPipe:
                return args.Shift ? "'" : "¨";
            case Keys.Space:
                return " ";
            case Keys.OemSemicolon:
                return args.Shift ? "\"" : "ù";
            case Keys.OemQuotes:
                return args.Shift ? "!" : "§";
            case Keys.OemTilde:
                return args.Shift ? "°" : ";";

            case Keys.Decimal:
                return args.Shift ? "" : ",";

            default:
                return base.KeyToString(args);
        }
    }

}