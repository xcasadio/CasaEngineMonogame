
namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
    public sealed class RankScaling<T> : ScalingAlgorithm<T>
    {

        internal double Alpha;



        public RankScaling(EvolutionObjective objective, double alpha)
            : base(objective)
        {
            string message = string.Empty;

            if (ValidateAlpha(alpha, ref message) == false)
            {
                throw new AiException("alpha", GetType().ToString(), message);
            }

            Alpha = alpha;
        }



        public override ScalingMapping<T> Scale(Population<T> population)
        {
            ScalingMapping<T> mapping;
            double temp;
            int popSize;

            //Create the mapping
            mapping = new ScalingMapping<T>(population.Genome.Count);

            //Order the population on inverse order depending on the objective
            if (Objective == EvolutionObjective.Maximize)
            {
                population.Genome.Sort(new ChromosomeComparer<T>(EvolutionObjective.Minimize));
            }

            else
            {
                population.Genome.Sort(new ChromosomeComparer<T>(EvolutionObjective.Maximize));
            }

            //Calculate the new fitness values (ranks) for the mapping
            popSize = population.Genome.Count;
            for (int i = 0; i < popSize; i++)
            {
                temp = ((2.0 - Alpha) / popSize) + (2.0 * i * (Alpha - 1.0) / (popSize * (popSize - 1.0)));
                mapping.AddChromosome(population[i], temp);
            }

            return mapping;
        }



        public static bool ValidateAlpha(double alpha, ref string message)
        {
            if (alpha <= 1.0 || alpha > 2.0)
            {
                message = "Alpha value must be in the interval (1, 2]";
                return false;
            }

            return true;
        }

    }
}
