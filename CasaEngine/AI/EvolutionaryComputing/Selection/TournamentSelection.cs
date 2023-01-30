using CasaEngine.AI.EvolutionaryComputing.Crossover;
using CasaEngine.AI.EvolutionaryComputing.Scaling;



namespace CasaEngine.AI.EvolutionaryComputing.Selection
{
    public sealed class TournamentSelection<T> : SelectionAlgorithm<T>
    {

        internal int size;



        public TournamentSelection(int numberParents, Random generator, EvolutionObjective objective, CrossoverMethod<T> crossover, ScalingMethod<T> scaling, int size)
            : base(numberParents, generator, objective, crossover, scaling)
        {
            String message = String.Empty;

            if (ValidateSize(size, ref message) == false)
                throw new AIException("size", this.GetType().ToString(), message);

            this.size = size;
        }



        protected override List<Chromosome<T>> Select()
        {
            List<Chromosome<T>> selectedChromosomes;

            //Select the chromosomes using tournaments
            selectedChromosomes = new List<Chromosome<T>>();
            for (int i = 0; i < this.numberParents; i++)
                selectedChromosomes.Add(Tournament(population));

            return selectedChromosomes;
        }

        private Chromosome<T> Tournament(Population<T> population)
        {
            int tournamentTry, selectedChromosome = 0;
            double bestValue;

            if (objective == EvolutionObjective.Maximize)
                bestValue = double.MinValue;

            else
                bestValue = double.MaxValue;

            //Search the winner chromosome
            for (int i = 0; i < size; i++)
            {
                //Select the chromosome that will participate in the tournament
                tournamentTry = generator.Next(0, population.Genome.Count - 1);

                //See if we´ve got a better individual
                if (objective == EvolutionObjective.Maximize)
                {
                    if (population[tournamentTry].Fitness > bestValue)
                    {
                        bestValue = population[tournamentTry].Fitness;
                        selectedChromosome = tournamentTry;
                    }
                }

                else
                {
                    if (population[tournamentTry].Fitness < bestValue)
                    {
                        bestValue = population[tournamentTry].Fitness;
                        selectedChromosome = tournamentTry;
                    }
                }
            }

            return (Chromosome<T>)population[selectedChromosome].Clone();
        }



        public bool ValidateSize(int size, ref String message)
        {
            if (size < 2)
            {
                message = "The size of the tournament must be greater than 1.";
                return false;
            }

            return true;
        }

    }
}
