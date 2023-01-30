namespace CasaEngine.AI.EvolutionaryComputing.Crossover
{
    public abstract class CrossoverAlgorithm<T>
    {

        protected internal double probability;

        protected internal Random generator;



        protected CrossoverAlgorithm(double probability, Random generator)
        {
            String message = String.Empty;

            //Validate values
            if (ValidateProbability(probability, ref message) == false)
                throw new AIException("probality", this.GetType().ToString(), message);

            if (ValidateGenerator(generator, ref message) == false)
                throw new AIException("generator", this.GetType().ToString(), message);

            this.probability = probability;
            this.generator = generator;
        }



        public virtual List<Chromosome<T>> Crossover(List<Chromosome<T>> parents)
        {
            if (generator.NextDouble() > probability)
                return parents;

            return null;
        }



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
