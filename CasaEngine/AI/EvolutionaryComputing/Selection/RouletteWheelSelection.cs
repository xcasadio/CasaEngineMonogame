using CasaEngine.AI.EvolutionaryComputing.Crossover;
using CasaEngine.AI.EvolutionaryComputing.Scaling;


namespace CasaEngine.AI.EvolutionaryComputing.Selection
{
    public sealed class RouletteWheelSelection<T> : SelectionAlgorithm<T>
    {

        public RouletteWheelSelection(int numberParents, Random generator, EvolutionObjective objective, CrossoverMethod<T> crossover, ScalingMethod<T> scaling)
            : base(numberParents, generator, objective, crossover, scaling)
        { }



        protected override List<Chromosome<T>> Select()
        {
            int i, j;
            double selectedFitness, total;
            List<Chromosome<T>> selectedChromosomes;

            //Select the winner chromosomes
            selectedChromosomes = new List<Chromosome<T>>();
            for (i = 0; i < this.numberParents; i++)
            {
                selectedFitness = generator.NextDouble() * scaledPopulation.TotalFitness;

                //Search for the winner chromosome
                total = 0;
                j = -1;
                while (total < selectedFitness)
                {
                    j++;
                    total += scaledPopulation.Fitness[j];
                }

                selectedChromosomes.Add((Chromosome<T>)scaledPopulation.Chromosomes[j].Clone());
            }

            return selectedChromosomes;
        }

    }
}
