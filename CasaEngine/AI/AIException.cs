namespace CasaEngine.AI
{
    public class AIException
        : SystemException
    {

        private readonly String fieldName;

        private readonly String className;

        private readonly String errorMessage;



        public override string Message => "Invalid value assigned to \"" + className + "." + fieldName + "\". The validation error was: " + errorMessage;


        public AIException(String fieldName, String className, String errorMessage)
        {
            this.fieldName = fieldName;
            this.className = className;
            this.errorMessage = errorMessage;
        }



    }
}
