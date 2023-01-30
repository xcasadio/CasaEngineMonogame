namespace HopField
{
    class Hopfield
    {

        int m_NumUnits;

        readonly List<int[]> m_Pattern = new List<int[]>();

        int[] m_Output;

        int[] m_Threshold;

        int[,] m_Weights;



        public int[] Output
        {
            get => m_Output;
            private set => m_Output = value;
        }






        public void GenerateNetwork(int num_)
        {
            int i;

            m_NumUnits = num_;
            m_Output = new int[num_];
            m_Threshold = new int[num_];
            m_Weights = new int[num_, num_];

            for (i = 0; i < num_; i++)
            {
                m_Threshold[i] = 0;
            }
        }

        public void AddPattern(int[] pattern_)
        {
            if (pattern_.Length != m_NumUnits)
            {
                throw new ArgumentException("HopField.SetInput() : input_.Length != m_NumUnits");
            }

            m_Pattern.Add(pattern_);
        }

        public void CalculateWeights()
        {
            int i, j, n;
            int Weight;

            for (i = 0; i < m_NumUnits; i++)
            {
                for (j = 0; j < m_NumUnits; j++)
                {
                    Weight = 0;

                    if (i != j)
                    {
                        for (n = 0; n < m_Pattern.Count; n++)
                        {
                            Weight += m_Pattern[n][i] * m_Pattern[n][j];
                        }
                    }

                    this.m_Weights[i, j] = Weight;
                }
            }
        }

        public void SetInput(ref int[] input_)
        {
            if (input_.Length != m_NumUnits)
            {
                throw new ArgumentException("HopField.SetInput() : input_.Length != m_NumUnits");
            }

            m_Output = input_;
        }



        private bool PropagateUnit(int i)
        {
            int j;
            int Sum, Out = 0;
            bool Changed;

            Changed = false;
            Sum = 0;

            for (j = 0; j < m_NumUnits; j++)
            {
                Sum += m_Weights[i, j] * m_Output[j];
            }

            if (Sum != m_Threshold[i])
            {
                if (Sum < m_Threshold[i]) Out = -1;
                if (Sum >= m_Threshold[i]) Out = 1;
                if (Out != m_Output[i])
                {
                    Changed = true;
                    m_Output[i] = Out;
                }
            }

            return Changed;
        }

        public void PropagateNet()
        {
            int Iteration, IterationOfLastChange;

            Iteration = 0;
            IterationOfLastChange = 0;

            Random rand = new Random();

            do
            {
                Iteration++;
                if (PropagateUnit(rand.Next(0, m_NumUnits - 1)))
                {
                    IterationOfLastChange = Iteration;
                }
            }
            while (Iteration - IterationOfLastChange < 10 * m_NumUnits);
        }


    }
}
