using System.Xml;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    public abstract class Parser
    {
        readonly Dictionary<string, CalculatorTokenBinaryOperator.BinaryOperator> _mapBinaryOperator = new Dictionary<string, CalculatorTokenBinaryOperator.BinaryOperator>();

        Dictionary<int, List<ParserToken>> _tokens = new Dictionary<int, List<ParserToken>>();

        readonly List<CalculatorToken> _calculatorList = new List<CalculatorToken>();
        readonly Calculator _calculator;

        readonly List<string> _tokensValue = new List<string>();



        internal CasaEngine.Design.Parser.Calculator Calculator => _calculator;

        internal string[] TokensValue => _tokensValue.ToArray();


        public Parser()
        {
            _mapBinaryOperator.Add("+", CalculatorTokenBinaryOperator.BinaryOperator.Plus);
            _mapBinaryOperator.Add("-", CalculatorTokenBinaryOperator.BinaryOperator.Minus);
            _mapBinaryOperator.Add("/", CalculatorTokenBinaryOperator.BinaryOperator.Divide);
            _mapBinaryOperator.Add("*", CalculatorTokenBinaryOperator.BinaryOperator.Multiply);
            _mapBinaryOperator.Add("==", CalculatorTokenBinaryOperator.BinaryOperator.Equal);
            _mapBinaryOperator.Add("!=", CalculatorTokenBinaryOperator.BinaryOperator.Different);
            _mapBinaryOperator.Add(">", CalculatorTokenBinaryOperator.BinaryOperator.Superior);
            _mapBinaryOperator.Add("<", CalculatorTokenBinaryOperator.BinaryOperator.Inferior);
            _mapBinaryOperator.Add(">=", CalculatorTokenBinaryOperator.BinaryOperator.SupEqual);
            _mapBinaryOperator.Add("<=", CalculatorTokenBinaryOperator.BinaryOperator.InfEqual);
            _mapBinaryOperator.Add("||", CalculatorTokenBinaryOperator.BinaryOperator.Or);
            _mapBinaryOperator.Add("&&", CalculatorTokenBinaryOperator.BinaryOperator.And);

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

            _calculator = new Calculator(this);
        }



        private void AddParserToken(ParserToken token, int priority)
        {
            if (_tokens.ContainsKey(priority) == true)
            {
                _tokens[priority].Add(token);
            }
            else
            {
                List<ParserToken> list = new List<ParserToken>();
                list.Add(token);
                _tokens.Add(priority, list);
            }
        }

        public void AddKeywordToken(string keyword)
        {
            AddParserToken(new ParserTokenKeyword(this, keyword), 11);
        }

        public void AddFunctionToken(string functionName)
        {
            AddParserToken(new ParserTokenFunction(this, functionName), 10);
        }

        public bool Check(string sentence)
        {
            sentence = sentence.Trim();

            if (string.IsNullOrEmpty(sentence) == true)
            {
                return false;
            }

            //trie le dictionnaire par priorité
            _tokens = _tokens.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach (KeyValuePair<int, List<ParserToken>> pair in _tokens)
            {
                foreach (ParserToken token in pair.Value)
                {
                    if (token.Check(sentence) == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool Compile(string sentence)
        {
            _calculatorList.Clear();

            if (Check(sentence) == true)
            {
                CalculatorToken root;
                CompileCalculatorToken(out root, 0);

                _calculator.Root = root;
                return true;
            }

            _calculatorList.Clear();
            _calculator.Root = null;

            return false;
        }

        public float Evaluate(string sentence)
        {
            sentence = sentence.ToLower();

            if (Compile(sentence) == true)
            {
                return _calculator.Evaluate();
            }

            throw new InvalidOperationException("Can't Compile!; Please check your sentence");
        }

        public float Evaluate()
        {
            if (_calculator.Root == null)
            {
                throw new InvalidOperationException("Compile before use Evaluate()");
            }

            return _calculator.Evaluate();
        }

        public abstract float EvaluateKeyword(string keyword);

        public abstract float EvaluateFunction(string functionName, string[] args);


        internal void AddToken(string token)
        {
            _tokensValue.Add(token);
        }

        private int CompileCalculatorToken(out CalculatorToken token, int index)
        {
            token = _calculatorList[index];

            if (token is CalculatorTokenBinaryOperator)
            {
                index = CompileBinaryOperator(token as CalculatorTokenBinaryOperator, index + 1);
            }
            else if (token is CalculatorTokenSequence)
            {
                CalculatorToken res;
                index = CompileSequence(token as CalculatorTokenSequence, out res, index + 1);
                token = res;
            }

            return index;
        }

        private int CompileBinaryOperator(CalculatorTokenBinaryOperator parent, int index)
        {
            CalculatorToken left, right;
            CompileCalculatorToken(out left, index);
            index = CompileCalculatorToken(out right, index + 1);

            parent.Left = left;
            parent.Right = right;

            return index + 1;
        }

        private int CompileSequence(CalculatorToken parent, out CalculatorToken res, int index)
        {
            CalculatorTokenSequence seq;
            int end = -1;
            res = null;

            for (int i = index; i < _calculatorList.Count; i++)
            {
                if (_calculatorList[i] is CalculatorTokenSequence)
                {
                    seq = (CalculatorTokenSequence)_calculatorList[i];

                    if (seq.Sequence == CalculatorTokenSequence.TokenSequence.StartSequence)
                    {
                        end = CompileCalculatorToken(out res, i + 1) + 1;
                        _calculatorList.RemoveRange(i, end - i);
                        return i;
                    }
                }
            }

            return end;
        }

        internal void AddCalculator(CalculatorToken token)
        {
            _calculatorList.Add(token);
        }

        internal CalculatorToken GetCalculatorByBinaryOperator(string @operator)
        {
            return new CalculatorTokenBinaryOperator(_calculator, _mapBinaryOperator[@operator]);
        }



        public void Save(XmlElement el, SaveOption option)
        {
            _calculator.Save(el, option);
        }

        public void Load(XmlElement el, SaveOption option)
        {
            _calculator.Load(el, option);
        }

        public void Save(BinaryWriter bw, SaveOption option)
        {
            _calculator.Save(bw, option);
        }

        public void Load(BinaryReader br, SaveOption option)
        {
            //_Calculator.Load(br_, option_);
        }


    }
}
