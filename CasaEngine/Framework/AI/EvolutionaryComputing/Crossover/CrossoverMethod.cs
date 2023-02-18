namespace CasaEngine.Framework.AI.EvolutionaryComputing.Crossover
{
    public delegate List<Chromosome<T>> CrossoverMethod<T>(List<Chromosome<T>> parents);
}
