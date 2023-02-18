namespace CasaEngine.Framework.AI.EvolutionaryComputing.Scaling
{
    public class ScalingMapping<T>
    {
        internal int position;
        internal double[] fitness;
        internal Chromosome<T>[] chromosomes;
        internal double minFitness;
        internal double totalFitness;

        public ScalingMapping(int size)
        {
            fitness = new double[size];
            chromosomes = new Chromosome<T>[size];

            minFitness = double.MaxValue;
            totalFitness = 0;
            position = 0;
        }

        public double[] Fitness => fitness;

        public Chromosome<T>[] Chromosomes => chromosomes;

        public double TotalFitness => totalFitness;

        public void AddChromosome(Chromosome<T> chromosome, double fitness)
        {
            //Adds the new values
            chromosomes[position] = chromosome;
            this.fitness[position] = fitness;

            //Checks to see if we have a new minimum value
            if (fitness < minFitness)
            {
                minFitness = fitness;
            }

            //Update total fitness and insert position
            totalFitness += fitness;
            position++;
        }

        public void Normalize()
        {
            if (minFitness < 0)
            {
                for (var i = 0; i < fitness.Length - 1; i++)
                {
                    fitness[i] -= minFitness;
                }
            }
        }

    }
}
