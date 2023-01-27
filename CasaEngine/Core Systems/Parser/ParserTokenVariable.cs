using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
	class ParserTokenVariable
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
		public ParserTokenVariable(Parser parser_)
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
			if (string.IsNullOrEmpty(sentence_) == true)
			{
				return true;
			}

			int c = (int)sentence_.ToCharArray(0, 1)[0];

			if (c >= (int)'0' || c <= (int)'9')
			{
				return false;
			}

			m_Token = sentence_;

			return true;
		}

        #endregion
	}
}
