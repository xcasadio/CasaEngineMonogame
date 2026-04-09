namespace CasaEngine.Framework.AI.Algorithms.EvolutionaryComputing;

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