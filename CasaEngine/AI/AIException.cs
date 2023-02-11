namespace CasaEngine.AI
{
    public class AiException
        : SystemException
    {

        private readonly String _fieldName;

        private readonly String _className;

        private readonly String _errorMessage;



        public override string Message => "Invalid value assigned to \"" + _className + "." + _fieldName + "\". The validation error was: " + _errorMessage;


        public AiException(String fieldName, String className, String errorMessage)
        {
            this._fieldName = fieldName;
            this._className = className;
            this._errorMessage = errorMessage;
        }



    }
}
