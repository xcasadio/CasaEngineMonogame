namespace CasaEngine.Front_End.Screen.Gadget
{
    public partial class ScreenGadgetLabel
    {

        public static int Num = 0;





        public ScreenGadgetLabel()
            : base("Label" + (Num++))
        {
            Width = 100;
            Height = 80;
        }



    }
}
