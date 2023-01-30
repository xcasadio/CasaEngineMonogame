using System.Xml;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    public abstract class Parser
    {
        readonly Dictionary<string, CalculatorTokenBinaryOperator.BinaryOperator> m_MapBinaryOperator = new Dictionary<string, CalculatorTokenBinaryOperator.BinaryOperator>();

        Dictionary<int, List<ParserToken>> m_Tokens = new Dictionary<int, List<ParserToken>>();

        readonly List<ICalculatorToken> m_CalculatorList = new List<ICalculatorToken>();
        readonly Calculator m_Calculator;

        readonly List<string> m_TokensValue = new List<string>();



        internal CasaEngine.Design.Parser.Calculator Calculator => m_Calculator;

        internal string[] TokensValue => m_TokensValue.ToArray();


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

        public void AddKeywordToken(string keyword_)
        {
            AddParserToken(new ParserTokenKeyword(this, keyword_), 11);
        }

        public void AddFunctionToken(string functionName_)
        {
            AddParserToken(new ParserTokenFunction(this, functionName_), 10);
        }

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

        public float Evaluate(string sentence_)
        {
            sentence_ = sentence_.ToLower();

            if (Compile(sentence_) == true)
            {
                return m_Calculator.Evaluate();
            }

            throw new InvalidOperationException("Can't Compile!; Please check your sentence");
        }

        public float Evaluate()
        {
            if (m_Calculator.Root == null)
            {
                throw new InvalidOperationException("Compile before use Evaluate()");
            }

            return m_Calculator.Evaluate();
        }

        public abstract float EvaluateKeyword(string keyword_);

        public abstract float EvaluateFunction(string functionName_, string[] args_);


        internal void AddToken(string token_)
        {
            m_TokensValue.Add(token_);
        }

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

        private int CompileBinaryOperator(CalculatorTokenBinaryOperator parent_, int index_)
        {
            ICalculatorToken left, right;
            CompileCalculatorToken(out left, index_);
            index_ = CompileCalculatorToken(out right, index_ + 1);

            parent_.Left = left;
            parent_.Right = right;

            return index_ + 1;
        }

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

        internal void AddCalculator(ICalculatorToken token_)
        {
            m_CalculatorList.Add(token_);
        }

        internal ICalculatorToken GetCalculatorByBinaryOperator(string operator_)
        {
            return new CalculatorTokenBinaryOperator(m_Calculator, m_MapBinaryOperator[operator_]);
        }



        public void Save(XmlElement el_, SaveOption option_)
        {
            m_Calculator.Save(el_, option_);
        }

        public void Load(XmlElement el_, SaveOption option_)
        {
            m_Calculator.Load(el_, option_);
        }

        public void Save(BinaryWriter bw_, SaveOption option_)
        {
            m_Calculator.Save(bw_, option_);
        }

        public void Load(BinaryReader br_, SaveOption option_)
        {
            //m_Calculator.Load(br_, option_);
        }


    }
}
