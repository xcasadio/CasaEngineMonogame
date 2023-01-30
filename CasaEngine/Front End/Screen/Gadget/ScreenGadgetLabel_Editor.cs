namespace CasaEngine.FrontEnd.Screen.Gadget
{
    public partial class ScreenGadgetLabel
    {

        public static int num = 0;





        public ScreenGadgetLabel()
            : base("Label" + (num++))
        {
            Width = 100;
            Height = 80;
        }



    }
}
