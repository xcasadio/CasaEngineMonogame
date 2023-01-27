using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
	/// <summary>
	/// 
	/// </summary>
	class ParserTokenUnaryOperator
		: ParserToken
	{
		#region Fields
        
        #endregion

        #region Properties

        #endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parser_"></param>
		/// <param name="token_"></param>
		public ParserTokenUnaryOperator(Parser parser_, string token_)
			: base(parser_, token_)
		{

		}

        #endregion

        #region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sentence_"></param>
		/// <returns></returns>
		public override bool Check(string sentence_)
		{
			if (sentence_.StartsWith(m_Token) == true)
			{
				Parser.Check(m_Token.Substring(1));
			}		

			return false;
		}

        #endregion
	}
}
