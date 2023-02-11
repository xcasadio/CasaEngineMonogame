
namespace CasaEngine.AI.EvolutionaryComputing.Mutation
{
    public abstract class MutationAlgorithm<T>
    {

        protected internal double Probability;

        protected internal Random Generator;



        protected MutationAlgorithm(double probability, Random generator)
        {
            String message = String.Empty;

            //Validate the params
            if (ValidateProbability(probability, ref message) == false)
                throw new AiException("probability", this.GetType().ToString(), message);

            if (ValidateGenerator(generator, ref message) == false)
                throw new AiException("generator", this.GetType().ToString(), message);

            this.Probability = probability;
            this.Generator = generator;
        }



        public abstract void Mutate(Population<T> population);



        public static bool ValidateProbability(double probability, ref String message)
        {
            if (probability < 0 || probability > 1)
            {
                message = "The probability value must be between 0.0 and 1.0";
                return false;
            }

            return true;
        }

        public static bool ValidateGenerator(Random generator, ref String message)
        {
            if (generator == null)
            {
                message = "The random number generator can´t be null.";
                return false;
            }

            return true;
        }

    }
}
