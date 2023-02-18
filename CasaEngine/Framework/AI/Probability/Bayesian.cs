namespace CasaEngine.Framework.AI.Probability
{
    class Pattern
        : IEquatable<Pattern>
    {
        readonly int[] _pattern;





        public Pattern(int[] pattern)
        {
            _pattern = pattern;
        }


        public bool Equals(Pattern other)
        {
            for (var i = 0; i < _pattern.Length; i++)
            {
                if (other._pattern[i] != _pattern[i])
                {
                    return false;
                }
            }

            return true;
        }


    }

    public class Bayesian
    {
        readonly int _nbActions;
        readonly int _nbPossibilities;
        readonly Dictionary<Pattern, int[]> _probabilities = new();
        readonly List<Pattern> _listPattern = new();





        public Bayesian(int nbAction, int nbPossibilities)
        {
            _nbActions = nbAction;
            _nbPossibilities = nbPossibilities;

            BuildProbabilities();
        }



        private void BuildProbabilities()
        {
            var list = new List<int[]>();
            var pattern = new int[_nbPossibilities];
            var proba = new int[_nbActions + 1];

            for (var i = 0; i < _nbActions + 1; i++)
            {
                proba[i] = 0;
            }

            for (var i = 0; i < _nbPossibilities; i++)
            {
                pattern[i] = 0;
            }

            CreatePattern(ref list, pattern, 1);

            foreach (var a in list)
            {
                var p = new Pattern(a);
                _listPattern.Add(p);
                _probabilities.Add(p, proba.ToArray());
            }
        }

        private void CreatePattern(ref List<int[]> listPattern, int[] pattern, int pos)
        {
            int p;

            listPattern.Add(pattern.ToArray());

            for (p = 0; p < pos; p++)
            {
                //TODO : quand p > 0 remettre a zero
                if (pattern[p] < _nbActions - 1)
                {
                    pattern[p]++;
                    break;
                }
            }

            //change
            if (p >= pos)
            {
                pattern[pos]++;

                for (var i = 0; i < pos; i++)
                {
                    pattern[i] = 0;
                }

                if (pattern[pos] > _nbActions - 1)
                {
                    pattern[pos] = 0;
                    pos++;

                    if (pos >= _nbPossibilities)
                    {
                        return;
                    }

                    pattern[pos]++;
                }
            }

            CreatePattern(ref listPattern, pattern, pos);
        }

        public int ComputeProbabilities(int[] currentPattern)
        {
            var pattern = GetPattern(currentPattern);

            var proba = _probabilities[pattern];
            var action = 0;
            double max = float.MinValue, x;

            for (var i = 0; i < _nbActions; i++)
            {
                x = proba[i] / (double)proba[_nbActions];
                if (max < x)
                {
                    max = x;
                    action = i;
                }
            }

            return action;
        }

        public void UpdateProbabilities(int[] currentPattern, int action)
        {
            var pattern = GetPattern(currentPattern);

            _probabilities[pattern][_nbActions]++;
            _probabilities[pattern][action]++;
        }

        Pattern GetPattern(int[] currentPattern)
        {
            var pattern = new Pattern(currentPattern);

            foreach (var p in _listPattern)
            {
                if (p.Equals(pattern))
                {
                    return p;
                }
            }

            throw new ArgumentException("Bayesian.GetPattern() : Can't find Pattern");
        }

    }
}
