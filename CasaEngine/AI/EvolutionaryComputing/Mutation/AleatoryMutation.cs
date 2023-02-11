
namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
    public sealed class AleatoryMutation : MutationAlgorithm<int>
    {

        internal int Floor;

        internal int Ceil;



        public AleatoryMutation(double probability, Random generator, int floor, int ceil)
            : base(probability, generator)
        {
            String message = String.Empty;

            //Validate params
            if (ValidateLimits(floor, ceil, ref message) == false)
                throw new AiException("floor-ceil", this.GetType().ToString(), message);

            this.Floor = floor;
            this.Ceil = ceil;
        }



        public override void Mutate(Population<int> population)
        {
            for (int i = 0; i < population.Genome.Count; i++)
                for (int j = 0; j < population[i].Genotype.Count; j++)
                    if (Generator.NextDouble() <= this.Probability)
                        population[i].Genotype[j] = Generator.Next(this.Floor, this.Ceil);
        }



        public static bool ValidateLimits(int floor, int ceil, ref String message)
        {
            if (floor > ceil)
            {
                message = "The floor value must be lower than the ceil value.";
                return false;
            }

            return true;
        }

    }
}
