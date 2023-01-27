#region Using Directives

using System;
using System.Collections.Generic;




#endregion

namespace CasaEngine.AI.Messaging
{
	/// <summary>
	/// This class comprares if 2 messages are equal
	/// </summary>
	/// <remarks>
	/// If two messages of the same type go to the same entity from the same entity and their time
	/// difference is lower or equal than the precision of the comparer , the two messages are equal 
	/// (used in the message manager to discard similar messages and avoid flowing an entity)
	///	</remarks>
	public class MessageComparer : IComparer<Message>
	{
		#region Fields

		/// <summary>
		/// Value that indicates the precision window of dispatch time we are using to compare 2 messages
		/// </summary>
		protected internal double precision;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="precision">Value that indicates the precision window of dispatch time we are using to compare 2 messages</param>
		public MessageComparer(double precision)
		{
			String message = String.Empty;

			if (ValidatePrecision(precision, ref message) == false)
				throw new AIException("precision", this.GetType().ToString(), message);

			this.precision = precision;
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the precision value is correct (>= 0)
		/// </summary>
		/// <param name="precision">The precision value we want to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidatePrecision(double precision, ref string message)
		{
			if (precision < 0)
			{
				message = "The precision value must be equal or greater than 0";
				return false;
			}

			return true;
		}

		#endregion

		#region IComparer<int> Members

		/// <summary>
		/// Compares two messages and returns their relative order
		/// </summary>
		/// <param name="x">First message to compare</param>
		/// <param name="y">Second message to compare</param>
		/// <returns>1 if x is greater than y, 0 if they are equal, -1 if y is greater than x</returns>
		public int Compare(Message x, Message y)
		{
			//Note to myself: it´s really a good idea to compare the extra info field? Maybe will let pass nearly similar messages...
			if ((x.SenderID == y.SenderID) && (x.RecieverID == y.RecieverID) && (x.Type == y.Type) && (x.ExtraInfo == y.ExtraInfo) && (System.Math.Abs(x.DispatchTime - y.DispatchTime) < precision))
				return 0;

			if (x.DispatchTime >= y.DispatchTime)
				return 1;

			else
				return -1;
		}

		#endregion
	}
}
