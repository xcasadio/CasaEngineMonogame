#region Using Directives

using System;




#endregion

namespace CasaEngine.AI.Messaging
{
	/// <summary>
	/// This class represents a message an entity can send to another one to communicate
	/// </summary>
	/// <remarks>
	/// Maybe the extraInfo field could be generic (to save us a cast), but I´m not sure that later the message handler functions and
	/// delegates would be possible to implement (handler functions surely yes and implementing IMessageable probably yes, but registering
	/// delegates to handle broadcast messages in the MessageManagerHandler seems to need the cast we were trying to save...)
	/// </remarks>
	[Serializable]
	public class Message
	{
		#region Constants

		/// <summary>
		/// This constant represents that the sender of the message is not important info
		/// </summary>
		public const int NoSenderID = -1;

		#endregion

		#region Fields

		/// <summary>
		/// The entity that sends the message
		/// </summary>
		protected internal int senderID;

		/// <summary>
		/// The entity that should recieve the message
		/// </summary>
		protected internal int recieverID;

		/// <summary>
		/// The message type
		/// </summary>
		protected internal int type;

		/// <summary>
		/// When to send the message (in ticks)
		/// </summary>
		protected internal double dispatchTime;

		/// <summary>
		/// Any other info associated with the message
		/// </summary>
		protected internal object extraInfo;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="senderID">The entity that sends the message</param>
		/// <param name="recieverID">The entity that should recieve the message</param>
		/// <param name="type">The message type</param>
		/// <param name="dispatchTime">When to send the message</param>
		/// <param name="extraInfo">Any other info associated with the message</param>
		public Message(int senderID, int recieverID, int type, double dispatchTime, object extraInfo)
		{
			String message = String.Empty;

			if (ValidateID(senderID, ref message) == false)
				throw new AIException("senderID", this.GetType().ToString(), message);

			if (ValidateID(recieverID, ref message) == false)
				throw new AIException("recieverID", this.GetType().ToString(), message);

			if (ValidateTime(dispatchTime, ref message) == false)
				throw new AIException("dispatchTime", this.GetType().ToString(), message);

			this.senderID = senderID;
			this.recieverID = recieverID;
			this.type = type;
			this.dispatchTime = dispatchTime;
			this.extraInfo = extraInfo;
		}

		#endregion

		#region Properties
		
		/// <summary>
		/// Gets or sets the sender ID
		/// </summary>
		public int SenderID
		{
			get { return senderID; }
			set
			{
				String message = String.Empty;

				if (ValidateID(value, ref message) == false)
					throw new AIException("senderID", this.GetType().ToString(), message);

				senderID = value;
			}
		}

		/// <summary>
		/// Gets or sets the reciever ID
		/// </summary>
		public int RecieverID
		{
			get { return recieverID; }
			set
			{
				String message = String.Empty;

				if (ValidateID(value, ref message) == false)
					throw new AIException("recieverID", this.GetType().ToString(), message);

				recieverID = value;
			}
		}

		/// <summary>
		/// Gets or sets the message type
		/// </summary>
		public int Type
		{
			get { return type; }
			set { type = value; }
		}

		/// <summary>
		/// Gets or sets the dispatch time
		/// </summary>
		public double DispatchTime
		{
			get { return dispatchTime; }
			set
			{
				String message = String.Empty;

				if (ValidateTime(value, ref message) == false)
					throw new AIException("dispatchTime", this.GetType().ToString(), message);

				dispatchTime = value;
			}
		}

		/// <summary>
		/// Gets or sets the extra info
		/// </summary>
		public object ExtraInfo
		{
			get { return extraInfo; }
			set { extraInfo = value; }
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the ID value is correct (>= -1)
		/// </summary>
		/// <param name="id">The ID value we want to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateID(int id, ref string message)
		{
			if (id < -1)
			{
				message = "ID must  be greater or equal than -1";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Validates if the dispatch time value is correct (>= 0)
		/// </summary>
		/// <param name="dispatchTime">The dispatch time value we want to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateTime(double dispatchTime, ref string message)
		{
			if (dispatchTime < 0)
			{
				message = "You can´t set a negative dispatch time (at least until we design a time travelling machine, should come after Jad)";
				return false;
			}

			return true;
		}

		#endregion
	}
}
