
namespace CasaEngine.AI.EvolutionaryComputing.Scaling
{
    public sealed class FitnessScaling<T> : ScalingAlgorithm<T>
    {

        public FitnessScaling(EvolutionObjective objective)
            : base(objective) { }



        public override ScalingMapping<T> Scale(Population<T> population)
        {
            ScalingMapping<T> mapping;
            double temp;

            //Create a new mapping
            mapping = new ScalingMapping<T>(population.Genome.Count);

            //Populate the mapping with the new fitness values
            for (int i = 0; i < population.Genome.Count; i++)
            {
                if (objective == EvolutionObjective.Maximize)
                    temp = population[i].Fitness;

                else
                    temp = 1 / population[i].Fitness;

                mapping.AddChromosome(population[i], temp);
            }

            return mapping;
        }

    }
}
