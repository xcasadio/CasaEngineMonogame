using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
	/// <summary>
	/// 
	/// </summary>
	class ParserTokenSequence
		: ParserToken
	{
		#region Fields

		static public readonly string sequence = "`";

        #endregion

        #region Properties

        #endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parser_"></param>
		public ParserTokenSequence(Parser parser_)
			: base(parser_, sequence)
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
			if (m_Token.Equals(sentence_) == true)
			{
				Parser.AddCalculator(new CalculatorTokenSequence(Parser.Calculator, CalculatorTokenSequence.TokenSequence.Sequence));
				return true;
			}

			return false;
		}

        #endregion
	}
}
