using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
	/// <summary>
	/// 
	/// </summary>
	class ParserTokenBinaryOperator
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
		public ParserTokenBinaryOperator(Parser parser_, string token_)
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
			int index = sentence_.IndexOf(m_Token);

			if (index != -1)
			{
				Parser.AddCalculator(Parser.GetCalculatorByBinaryOperator(m_Token));

				string s1, s2;
				s1 = sentence_.Substring(0, index);
				s2 = sentence_.Substring(index + m_Token.Length, sentence_.Length - index - m_Token.Length);

				return Parser.Check(s1) && Parser.Check(s2);
			}

			return false;
		}

        #endregion
	}
}
