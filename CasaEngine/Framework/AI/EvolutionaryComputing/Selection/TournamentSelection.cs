using CasaEngine.Framework.AI.EvolutionaryComputing.Crossover;
using CasaEngine.Framework.AI.EvolutionaryComputing.Scaling;

namespace CasaEngine.Framework.AI.EvolutionaryComputing.Selection
{
    public sealed class TournamentSelection<T> : SelectionAlgorithm<T>
    {

        internal int Size;



        public TournamentSelection(int numberParents, Random generator, EvolutionObjective objective, CrossoverMethod<T> crossover, ScalingMethod<T> scaling, int size)
            : base(numberParents, generator, objective, crossover, scaling)
        {
            var message = string.Empty;

            if (ValidateSize(size, ref message) == false)
            {
                throw new AiException("size", GetType().ToString(), message);
            }

            Size = size;
        }



        protected override List<Chromosome<T>> Select()
        {
            List<Chromosome<T>> selectedChromosomes;

            //Select the chromosomes using tournaments
            selectedChromosomes = new List<Chromosome<T>>();
            for (var i = 0; i < NumberParents; i++)
            {
                selectedChromosomes.Add(Tournament(Population));
            }

            return selectedChromosomes;
        }

        private Chromosome<T> Tournament(Population<T> population)
        {
            int tournamentTry, selectedChromosome = 0;
            double bestValue;

            if (Objective == EvolutionObjective.Maximize)
            {
                bestValue = double.MinValue;
            }

            else
            {
                bestValue = double.MaxValue;
            }

            //Search the winner chromosome
            for (var i = 0; i < Size; i++)
            {
                //Select the chromosome that will participate in the tournament
                tournamentTry = Generator.Next(0, population.Genome.Count - 1);

                //See if we´ve got a better individual
                if (Objective == EvolutionObjective.Maximize)
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



        public bool ValidateSize(int size, ref string message)
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
