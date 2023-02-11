using CasaEngine.AI.EvolutionaryComputing.Crossover;
using CasaEngine.AI.EvolutionaryComputing.Scaling;


namespace CasaEngine.AI.EvolutionaryComputing.Selection
{
    public sealed class SusSelection<T> : SelectionAlgorithm<T>
    {

        public SusSelection(int numberParents, Random generator, EvolutionObjective objective, CrossoverMethod<T> crossover, ScalingMethod<T> scaling)
            : base(numberParents, generator, objective, crossover, scaling)
        { }



        protected override List<Chromosome<T>> Select()
        {
            int i, j;
            double selectedFitness, increment, total;
            List<Chromosome<T>> selectedChromosomes;

            //Calculate the spaced hand
            increment = ScaledPopulation.TotalFitness / (double)this.NumberParents;

            //Get the start position
            selectedChromosomes = new List<Chromosome<T>>();
            selectedFitness = Generator.NextDouble() * increment;

            //Select the winner chromosomes
            total = 0;
            j = 0;
            for (i = 0; i < this.NumberParents; i++)
            {
                //Search the selected chromosome
                while (total < selectedFitness)
                {
                    j++;
                    total += ScaledPopulation.Fitness[j];
                }

                selectedChromosomes.Add((Chromosome<T>)ScaledPopulation.Chromosomes[j].Clone());

                //Increment the position by the spaced hand size
                selectedFitness += increment;
            }

            return selectedChromosomes;
        }

    }
}
