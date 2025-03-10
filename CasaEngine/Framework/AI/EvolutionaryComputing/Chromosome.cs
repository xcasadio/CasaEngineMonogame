using System.Runtime.Serialization.Formatters.Binary;

namespace CasaEngine.Framework.AI.EvolutionaryComputing;

[Serializable]
public class Chromosome<T> : ICloneable
{

    protected internal double fitness;

    protected internal List<T> genotype;

    public Chromosome()
    {
        genotype = new List<T>();
    }

    public virtual double Fitness
    {
        get => fitness;
        set => fitness = value;
    }

    public virtual List<T> Genotype
    {
        get => genotype;
        set
        {
            var message = string.Empty;

            if (ValidateGenotype(value, ref message) == false)
            {
                throw new AiException("genotype", GetType().ToString(), message);
            }

            genotype = value;
        }
    }

    public virtual T this[int index]
    {
        get => genotype[index];
        set => genotype[index] = value;
    }

    public object Clone()
    {
        Chromosome<T> newChrom;
        MemoryStream memory;
        object clone;

        //Best scenario, T is a ValueType
        if (typeof(T).IsValueType)
        {
            newChrom = (Chromosome<T>)MemberwiseClone();
            newChrom.Genotype.Clear();

            for (var i = 0; i < genotype.Count; i++)
            {
                newChrom.Genotype.Add(genotype[i]);
            }

            return newChrom;
        }

        //So so scenario, T implements ICloneable, lots of casts needed
        if (typeof(T).GetInterface("System.ICloneable", true) != null)
        {
            newChrom = (Chromosome<T>)MemberwiseClone();
            newChrom.Genotype.Clear();

            for (var i = 0; i < genotype.Count; i++)
            {
                newChrom.Genotype.Add((T)((ICloneable)genotype[i]).Clone());
            }

            return newChrom;
        }

        //It wasn�t possible to clone the object, return ourself
        return this;
    }

    public virtual Chromosome<T> FastEmptyInstance()
    {
        Chromosome<T> clone;

        //Clone the actual chromosome
        clone = (Chromosome<T>)MemberwiseClone();

        //Restart the internal fields of the chromosome
        clone.Fitness = 0;
        clone.Genotype = new List<T>();

        return clone;
    }

    public static bool ValidateChromosome(Chromosome<T> chromosome, ref string message)
    {
        if (chromosome == null)
        {
            message = "The chromosome can�t be null.";
            return false;
        }

        if (ValidateGenotype(chromosome.Genotype, ref message) == false)
        {
            return false;
        }

        return true;
    }

    public static bool ValidateGenotype(List<T> genotype, ref string message)
    {
        if (genotype == null)
        {
            message = "The genotype list can�t be null.";
            return false;
        }

        return true;
    }

}