using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    /// <summary>
    /// Recognized operators : 
    /// +, -, /, *, ==, !=, >, &lt;, >=, &lt;=, ||, &&
    /// 
    /// Can support functions, format : function_name="arg1, arg2, ..."
    /// Can support keyword
    /// </summary>
    public abstract class Parser
    {

        Dictionary<string, CalculatorTokenBinaryOperator.BinaryOperator> m_MapBinaryOperator = new Dictionary<string, CalculatorTokenBinaryOperator.BinaryOperator>();

        Dictionary<int, List<ParserToken>> m_Tokens = new Dictionary<int, List<ParserToken>>();

        List<ICalculatorToken> m_CalculatorList = new List<ICalculatorToken>();
        Calculator m_Calculator;

        List<string> m_TokensValue = new List<string>();



        /// <summary>
        /// 
        /// </summary>
        internal CasaEngine.Design.Parser.Calculator Calculator
        {
            get { return m_Calculator; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        internal string[] TokensValue
        {
            get { return m_TokensValue.ToArray(); }
        }



        /// <summary>
        /// 
        /// </summary>
        public Parser()
        {
            m_MapBinaryOperator.Add("+", CalculatorTokenBinaryOperator.BinaryOperator.Plus);
            m_MapBinaryOperator.Add("-", CalculatorTokenBinaryOperator.BinaryOperator.Minus);
            m_MapBinaryOperator.Add("/", CalculatorTokenBinaryOperator.BinaryOperator.Divide);
            m_MapBinaryOperator.Add("*", CalculatorTokenBinaryOperator.BinaryOperator.Multiply);
            m_MapBinaryOperator.Add("==", CalculatorTokenBinaryOperator.BinaryOperator.Equal);
            m_MapBinaryOperator.Add("!=", CalculatorTokenBinaryOperator.BinaryOperator.Different);
            m_MapBinaryOperator.Add(">", CalculatorTokenBinaryOperator.BinaryOperator.Superior);
            m_MapBinaryOperator.Add("<", CalculatorTokenBinaryOperator.BinaryOperator.Inferior);
            m_MapBinaryOperator.Add(">=", CalculatorTokenBinaryOperator.BinaryOperator.SupEqual);
            m_MapBinaryOperator.Add("<=", CalculatorTokenBinaryOperator.BinaryOperator.InfEqual);
            m_MapBinaryOperator.Add("||", CalculatorTokenBinaryOperator.BinaryOperator.Or);
            m_MapBinaryOperator.Add("&&", CalculatorTokenBinaryOperator.BinaryOperator.And);

            AddParserToken(new ParserTokenSequence(this), 1);
            AddParserToken(new ParserTokenDelimiter(this, "(", ")"), 2);
            AddParserToken(new ParserTokenBinaryOperator(this, "*"), 4);
            AddParserToken(new ParserTokenBinaryOperator(this, "/"), 4);
            AddParserToken(new ParserTokenBinaryOperator(this, "+"), 5);
            AddParserToken(new ParserTokenBinaryOperator(this, "-"), 5);
            AddParserToken(new ParserTokenBinaryOperator(this, "<"), 6);
            AddParserToken(new ParserTokenBinaryOperator(this, "<="), 6);
            AddParserToken(new ParserTokenBinaryOperator(this, ">="), 6);
            AddParserToken(new ParserTokenBinaryOperator(this, ">"), 6);
            AddParserToken(new ParserTokenBinaryOperator(this, "=="), 7);
            AddParserToken(new ParserTokenBinaryOperator(this, "!="), 7);
            //AddToken(new ParserTokenOperator(this, "^"), 8);
            AddParserToken(new ParserTokenBinaryOperator(this, "&&"), 9);
            AddParserToken(new ParserTokenBinaryOperator(this, "||"), 9);
            //AddToken(new ParserTokenKeyword(this, ""), 10);
            AddParserToken(new ParserTokenValue(this), 12);

            m_Calculator = new Calculator(this);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="token_"></param>
        /// <param name="priority_">Plus la valeur est petite plus la priorite est grande</param>
        private void AddParserToken(ParserToken token_, int priority_)
        {
            if (m_Tokens.ContainsKey(priority_) == true)
            {
                m_Tokens[priority_].Add(token_);
            }
            else
            {
                List<ParserToken> list = new List<ParserToken>();
                list.Add(token_);
                m_Tokens.Add(priority_, list);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyword_"></param>
        public void AddKeywordToken(string keyword_)
        {
            AddParserToken(new ParserTokenKeyword(this, keyword_), 11);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="functionName_"></param>
        public void AddFunctionToken(string functionName_)
        {
            AddParserToken(new ParserTokenFunction(this, functionName_), 10);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentence_"></param>
        /// <returns></returns>
        public bool Check(string sentence_)
        {
            sentence_ = sentence_.Trim();

            if (string.IsNullOrEmpty(sentence_) == true)
            {
                return false;
            }

            //trie le dictionnaire par priorité
            m_Tokens = m_Tokens.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach (KeyValuePair<int, List<ParserToken>> pair in m_Tokens)
            {
                foreach (ParserToken token in pair.Value)
                {
                    if (token.Check(sentence_) == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentence_"></param>
        /// <returns></returns>
        public bool Compile(string sentence_)
        {
            m_CalculatorList.Clear();

            if (Check(sentence_) == true)
            {
                ICalculatorToken root;
                CompileCalculatorToken(out root, 0);

                m_Calculator.Root = root;
                return true;
            }

            m_CalculatorList.Clear();
            m_Calculator.Root = null;

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentence_"></param>
        /// <returns></returns>
        public float Evaluate(string sentence_)
        {
            sentence_ = sentence_.ToLower();

            if (Compile(sentence_) == true)
            {
                return m_Calculator.Evaluate();
            }

            throw new InvalidOperationException("Can't Compile!; Please check your sentence");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public float Evaluate()
        {
            if (m_Calculator.Root == null)
            {
                throw new InvalidOperationException("Compile before use Evaluate()");
            }

            return m_Calculator.Evaluate();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyword_"></param>
        /// <returns></returns>
        public abstract float EvaluateKeyword(string keyword_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="functionName_"></param>
        /// <param name="args_"></param>
        /// <returns></returns>
        public abstract float EvaluateFunction(string functionName_, string[] args_);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="token_"></param>
        internal void AddToken(string token_)
        {
            m_TokensValue.Add(token_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token_"></param>
        /// <param name="index_"></param>
        /// <returns></returns>
        private int CompileCalculatorToken(out ICalculatorToken token_, int index_)
        {
            token_ = m_CalculatorList[index_];

            if (token_ is CalculatorTokenBinaryOperator)
            {
                index_ = CompileBinaryOperator(token_ as CalculatorTokenBinaryOperator, index_ + 1);
            }
            else if (token_ is CalculatorTokenSequence)
            {
                ICalculatorToken res;
                index_ = CompileSequence(token_ as CalculatorTokenSequence, out res, index_ + 1);
                token_ = res;
            }

            return index_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent_"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private int CompileBinaryOperator(CalculatorTokenBinaryOperator parent_, int index_)
        {
            ICalculatorToken left, right;
            CompileCalculatorToken(out left, index_);
            index_ = CompileCalculatorToken(out right, index_ + 1);

            parent_.Left = left;
            parent_.Right = right;

            return index_ + 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent_"></param>
        /// <param name="res_"></param>
        /// <param name="index_"></param>
        /// <returns>the index of the end of the sequence</returns>
        private int CompileSequence(ICalculatorToken parent_, out ICalculatorToken res_, int index_)
        {
            CalculatorTokenSequence seq;
            int end = -1;
            res_ = null;

            for (int i = index_; i < m_CalculatorList.Count; i++)
            {
                if (m_CalculatorList[i] is CalculatorTokenSequence)
                {
                    seq = (CalculatorTokenSequence)m_CalculatorList[i];

                    if (seq.Sequence == CalculatorTokenSequence.TokenSequence.StartSequence)
                    {
                        end = CompileCalculatorToken(out res_, i + 1) + 1;
                        m_CalculatorList.RemoveRange(i, end - i);
                        return i;
                    }
                }
            }

            return end;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token_"></param>
        internal void AddCalculator(ICalculatorToken token_)
        {
            m_CalculatorList.Add(token_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="operator_"></param>
        /// <returns></returns>
        internal ICalculatorToken GetCalculatorByBinaryOperator(string operator_)
        {
            return new CalculatorTokenBinaryOperator(m_Calculator, m_MapBinaryOperator[operator_]);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Save(XmlElement el_, SaveOption option_)
        {
            m_Calculator.Save(el_, option_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Load(XmlElement el_, SaveOption option_)
        {
            m_Calculator.Load(el_, option_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Save(BinaryWriter bw_, SaveOption option_)
        {
            m_Calculator.Save(bw_, option_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Load(BinaryReader br_, SaveOption option_)
        {
            //m_Calculator.Load(br_, option_);
        }


    }
}
