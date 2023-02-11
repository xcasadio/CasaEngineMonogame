
namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
    public class ScalingMapping<T>
    {

        internal int Position;

        internal double[] Fitness;

        internal Chromosome<T>[] Chromosomes;

        internal double MinFitness;

        internal double TotalFitness;



        public ScalingMapping(int size)
        {
            fitness = new double[size];
            chromosomes = new Chromosome<T>[size];

            MinFitness = double.MaxValue;
            totalFitness = 0;
            Position = 0;
        }



        public double[] Fitness => fitness;

        public Chromosome<T>[] Chromosomes => chromosomes;

        public double TotalFitness => totalFitness;


        public void AddChromosome(Chromosome<T> chromosome, double fitness)
        {
            //Adds the new values
            this.chromosomes[Position] = chromosome;
            this.fitness[Position] = fitness;

            //Checks to see if we have a new minimum value
            if (fitness < MinFitness)
                MinFitness = fitness;

            //Update total fitness and insert position
            totalFitness += fitness;
            Position++;
        }

        public void Normalize()
        {
            if (MinFitness < 0)
                for (int i = 0; i < fitness.Length - 1; i++)
                    fitness[i] -= MinFitness;
        }

    }
}
