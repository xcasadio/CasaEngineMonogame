namespace CasaEngine.Framework.AI.EvolutionaryComputing.Replacement
{
    public delegate Population<T> ReplacementMethod<T>(Population<T> parents, Population<T> children);
}
