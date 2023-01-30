using CasaEngine.AI.EvolutionaryComputing.Crossover;
using CasaEngine.AI.EvolutionaryComputing.Scaling;



namespace CasaEngine.AI.EvolutionaryComputing.Selection
{
    public abstract class SelectionAlgorithm<T>
    {

        protected internal int numberParents;

        protected internal Random generator;

        protected internal EvolutionObjective objective;

        protected internal CrossoverMethod<T> crossover;

        protected internal ScalingMethod<T> scaling;

        protected internal Population<T> population;

        protected internal ScalingMapping<T> scaledPopulation;



        public SelectionAlgorithm(int numberParents, Random generator, EvolutionObjective objective, CrossoverMethod<T> crossover, ScalingMethod<T> scaling)
        {
            String message = String.Empty;

            if (ValidateNumberParents(numberParents, ref message) == false)
                throw new AIException("numberParents", this.GetType().ToString(), message);

            if (ValidateGenerator(generator, ref message) == false)
                throw new AIException("generator", this.GetType().ToString(), message);

            if (ValidateCrossover(crossover, ref message) == false)
                throw new AIException("crossover", this.GetType().ToString(), message);

            this.numberParents = numberParents;
            this.generator = generator;
            this.objective = objective;
            this.crossover = crossover;
            this.scaling = scaling;
        }



        public Population<T> Selection(Population<T> population, int offspringPopulationSize)
        {
            Population<T> offsprings;
            List<Chromosome<T>> selected;

            //Save the parents
            this.population = population;

            offsprings = population.FastEmptyInstance();

            //Test if scaling is used
            if (scaling != null)
                scaledPopulation = scaling(population);

            //Generate the offsprings
            while (offsprings.Genome.Count < offspringPopulationSize)
            {
                selected = Select();
                offsprings.Genome.AddRange(crossover(selected));
            }

            //Eliminate the excess offsprings
            if (offsprings.Genome.Count > offspringPopulationSize)
            {
                while (offsprings.Genome.Count > offspringPopulationSize)
                    offsprings.Genome.RemoveAt(offsprings.Genome.Count - 1);
            }

            return offsprings;
        }

        protected abstract List<Chromosome<T>> Select();



        public bool ValidateNumberParents(int numberParents, ref String message)
        {
            if (numberParents < 2)
            {
                message = "The number of selected parents must be greater than 1.";
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

        public static bool ValidateCrossover(CrossoverMethod<T> crossover, ref string message)
        {
            if (crossover == null)
            {
                message = "The crossover operator can´t be null";
                return false;
            }

            return true;
        }

    }
}
