namespace CasaEngine.Framework.AI.EvolutionaryComputing.Crossover;

public abstract class CrossoverAlgorithm<T>
{

    protected internal double Probability;

    protected internal Random Generator;

    protected CrossoverAlgorithm(double probability, Random generator)
    {
        var message = string.Empty;

        //Validate values
        if (ValidateProbability(probability, ref message) == false)
        {
            throw new AiException("probality", GetType().ToString(), message);
        }

        if (ValidateGenerator(generator, ref message) == false)
        {
            throw new AiException("generator", GetType().ToString(), message);
        }

        Probability = probability;
        Generator = generator;
    }

    public virtual List<Chromosome<T>> Crossover(List<Chromosome<T>> parents)
    {
        if (Generator.NextDouble() > Probability)
        {
            return parents;
        }

        return null;
    }

    public static bool ValidateProbability(double probability, ref string message)
    {
        if (probability < 0 || probability > 1)
        {
            message = "The probability value must be between 0.0 and 1.0";
            return false;
        }

        return true;
    }

    public static bool ValidateGenerator(Random generator, ref string message)
    {
        if (generator == null)
        {
            message = "The random number generator can�t be null.";
            return false;
        }

        return true;
    }

}