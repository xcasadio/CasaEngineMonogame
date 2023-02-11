namespace CasaEngine.AI
{
    public class AiException : SystemException
    {
        private readonly string _fieldName;

        private readonly string _className;

        private readonly string _errorMessage;

        public override string Message => "Invalid value assigned to \"" + _className + "." + _fieldName + "\". The validation error was: " + _errorMessage;

        public AiException(string fieldName, string className, string errorMessage)
        {
            _fieldName = fieldName;
            _className = className;
            _errorMessage = errorMessage;
        }
    }
}
