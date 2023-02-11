using System.Runtime.Serialization.Formatters.Binary;





namespace CasaEngine.AI.EvolutionaryComputing
{
    [Serializable]
    public class Chromosome<T> : ICloneable
    {

        protected internal double Fitness;

        protected internal List<T> Genotype;



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
                String message = String.Empty;

                if (ValidateGenotype(value, ref message) == false)
                    throw new AiException("genotype", this.GetType().ToString(), message);

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
            BinaryFormatter formater;
            object clone;

            //Best scenario, T is a ValueType
            if (typeof(T).IsValueType)
            {
                newChrom = (Chromosome<T>)MemberwiseClone();
                newChrom.Genotype.Clear();

                for (int i = 0; i < genotype.Count; i++)
                    newChrom.Genotype.Add(genotype[i]);

                return newChrom;
            }

            //So so scenario, T implements ICloneable, lots of casts needed
            if (typeof(T).GetInterface("System.ICloneable", true) != null)
            {
                newChrom = (Chromosome<T>)MemberwiseClone();
                newChrom.Genotype.Clear();

                for (int i = 0; i < genotype.Count; i++)
                    newChrom.Genotype.Add((T)((ICloneable)genotype[i]).Clone());

                return newChrom;
            }

            // Worst scenario, serialization (SLOW) is needed
            if (typeof(T).IsSerializable)
            {
                using (memory = new MemoryStream())
                {
                    //Serialize ourselves
                    formater = new BinaryFormatter();
                    formater.Serialize(memory, this);

                    //Move the memory buffer to the start
                    memory.Seek(0, SeekOrigin.Begin);

                    //Undo the serialization in the new clone object
                    clone = formater.Deserialize(memory);

                    return clone;
                }
            }

            //It wasn´t possible to clone the object, return ourself
            return this;
        }

        public virtual Chromosome<T> FastEmptyInstance()
        {
            Chromosome<T> clone;

            //Clone the actual chromosome
            clone = (Chromosome<T>)base.MemberwiseClone();

            //Restart the internal fields of the chromosome
            clone.Fitness = 0;
            clone.Genotype = new List<T>();

            return clone;
        }



        public static bool ValidateChromosome(Chromosome<T> chromosome, ref string message)
        {
            if (chromosome == null)
            {
                message = "The chromosome can´t be null.";
                return false;
            }

            if (ValidateGenotype(chromosome.Genotype, ref message) == false)
                return false;

            return true;
        }

        public static bool ValidateGenotype(List<T> genotype, ref String message)
        {
            if (genotype == null)
            {
                message = "The genotype list can´t be null.";
                return false;
            }

            return true;
        }

    }
}
