using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
    /// <summary>
    /// format : function_name="arg1, arg2, ..."
    /// </summary>
    class ParserTokenFunction
        : ParserToken
    {





        /// <summary>
        /// 
        /// </summary>
        /// <param name="parser_"></param>
        /// <param name="token_"></param>
        public ParserTokenFunction(Parser parser_, string token_)
            : base(parser_, token_)
        { }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentence_"></param>
        /// <returns></returns>
        public override bool Check(string sentence_)
        {
            if (sentence_.StartsWith(m_Token.ToLower()) == true)
            {
                List<string> args = new List<string>();
                string str = sentence_.Replace(m_Token + "=", "");
                string s2 = string.Empty;

                if (str.StartsWith("\"") == true)
                {
                    str = str.Substring(1);

                    int index = str.IndexOf("\"");

                    if (index != -1)
                    {
                        string argument = str.Substring(0, index);
                        string[] a = argument.Split(',');

                        s2 = str.Substring(index + 1, str.Length - index - 1);

                        foreach (string s in a)
                        {
                            args.Add(s);
                        }
                    }
                    else
                    {
                        //error
                        return false;
                    }
                }

                Parser.AddCalculator(new CalculatorTokenFunction(Parser.Calculator, m_Token, args.ToArray()));

                /*if (string.IsNullOrEmpty(s2) == false)
				{
					return Parser.Check(s2);
				}
				else
				{
					return true;
				}*/

                return true;
            }

            return false;
        }

    }
}
