namespace CasaEngine.Framework.AI.EvolutionaryComputing
{
    public interface IEvolutionaryProblem<T>
    {

        EvolutionObjective Objective
        {
            get;
            set;
        }



        Population<T> GenerateInitialPopulation();

        void CalculateFitness(Population<T> population);

    }
}
