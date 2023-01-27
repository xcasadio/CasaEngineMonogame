using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI
{
    /// <summary>
    /// 
    /// </summary>
    public class AIException
    	: SystemException
	{
		#region Fields

		/// <summary>
		/// The field that received the invalid value
		/// </summary>
		private String fieldName;

		/// <summary>
		/// The class where the field is located
		/// </summary>
		private String className;

		/// <summary>
		/// The validation error message
		/// </summary>
		private String errorMessage;

		#endregion

		#region Properties

		/// <summary>
		/// Gets the validation error message
		/// </summary>
		public override string Message
		{
			get
			{
				return "Invalid value assigned to \"" + className + "." + fieldName + "\". The validation error was: " + errorMessage;
			}
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="fieldName">The field that received the invalid value</param>
		/// <param name="className">The class where the field is located</param>
		/// <param name="errorMessage">The validation error message</param>
        public AIException(String fieldName, String className, String errorMessage)
		{
			this.fieldName = fieldName;
			this.className = className;
			this.errorMessage = errorMessage;
		}

		#endregion

		#region Methods

		#endregion
	}
}
