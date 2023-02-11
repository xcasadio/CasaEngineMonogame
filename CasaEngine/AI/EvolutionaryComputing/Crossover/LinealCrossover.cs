namespace CasaEngine.AI.EvolutionaryComputing.Crossover
{
    public sealed class LinealCrossover : CrossoverAlgorithm<double>
    {

        internal double IntervalModifier;



        public LinealCrossover(double probability, Random generator, double intervalModifier)
            : base(probability, generator)
        {
            String message = String.Empty;

            if (ValidateIntervalModifier(intervalModifier, ref message) == false)
                throw new AiException("intervalModifier", this.GetType().ToString(), message);

            this.IntervalModifier = intervalModifier;
        }



        public override List<Chromosome<double>> Crossover(List<Chromosome<double>> parents)
        {
            List<Chromosome<double>> list;
            Chromosome<double> chromosome1, chromosome2;
            double scaleFactor = 0;

            //This algorithm uses only 2 parents
            if (parents.Count != 2)
                throw new Exception("The number of parents must be 2.");

            list = new List<Chromosome<double>>();

            //Test to see if there´s a crossover or not
            if (base.Crossover(parents) != null)
            {
                list.Add((Chromosome<double>)parents[0].Clone());
                list.Add((Chromosome<double>)parents[1].Clone());

                return list;
            }

            chromosome1 = parents[0].FastEmptyInstance();
            chromosome2 = parents[0].FastEmptyInstance();

            //Calculate the genotype of each chromosome. The scale factor is calcultated every time
            for (int i = 0; i < parents[0].Genotype.Count; i++)
            {
                scaleFactor = Generator.NextDouble() * (1 - 2 * IntervalModifier) + IntervalModifier;

                chromosome1.Genotype.Add(parents[0][i] * scaleFactor + parents[0][i] * (1 - scaleFactor));
                chromosome2.Genotype.Add(parents[0][i] * (1 - scaleFactor) + parents[0][i] * scaleFactor);
            }

            list.Add(chromosome1);
            list.Add(chromosome2);

            return list;
        }



        public static bool ValidateIntervalModifier(double intervalModifier, ref string message)
        {
            if (intervalModifier < 0)
            {
                message = "The interval modifier must be equal or greater than 0.";
                return false;
            }

            return true;
        }

    }
}
