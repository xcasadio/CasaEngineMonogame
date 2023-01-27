using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
	class ParserTokenValue
		: ParserToken
	{
		#region Fields

		float m_Value;

        #endregion

        #region Properties

        #endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parser_"></param>
		public ParserTokenValue(Parser parser_)
			: base(parser_, string.Empty)
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
            //int value;

			if (string.IsNullOrEmpty(sentence_) == true)
			{
				return false;
			}

			if (float.TryParse(sentence_, out m_Value) == true)
			{
				Parser.AddCalculator(new CalculatorTokenValue(Parser.Calculator, m_Value));
			}
            else
			{
				Parser.AddCalculator(new CalculatorTokenValue(Parser.Calculator, sentence_));
			}

			m_Token = sentence_;
			

			return true;
		}

        #endregion
	}
}
