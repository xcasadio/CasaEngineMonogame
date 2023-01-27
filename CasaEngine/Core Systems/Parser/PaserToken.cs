using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
    /// <summary>
    /// Mother for all token than the Parser can identify
    /// </summary>
	abstract class ParserToken
	{
		#region Fields

		protected string m_Token = string.Empty;
		Parser m_Parser;

        #endregion

        #region Properties

		/// <summary>
		/// Gets
		/// </summary>
		protected Parser Parser
		{
			get { return m_Parser; }
		}

        #endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parser_"></param>
		/// <param name="token_"></param>
		protected ParserToken(Parser parser_, string token_)
		{
			parser_.AddToken(token_);

			m_Token = token_;
			m_Parser = parser_;
		}

        #endregion

        #region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sentence_"></param>
		/// <returns></returns>
		public abstract bool Check(string sentence_);

        #endregion
	}
}
