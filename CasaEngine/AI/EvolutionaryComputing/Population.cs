namespace CasaEngine.AI.EvolutionaryComputing
{
    [Serializable]
    public class Population<T> : ICloneable
    {
        protected internal List<Chromosome<T>> genome;
        protected internal bool hasPerfectSolution;
        protected internal int perfectSolutionIndex;

        public Population()
        {
            genome = new List<Chromosome<T>>();
            hasPerfectSolution = false;
            perfectSolutionIndex = -1;
        }

        public virtual List<Chromosome<T>> Genome
        {
            get => genome;
            set
            {
                var message = string.Empty;

                if (ValidateGenome(value, ref message) == false)
                {
                    throw new AiException("genome", GetType().ToString(), message);
                }

                genome = value;
            }
        }

        public virtual double TotalFitness
        {
            get
            {
                double total = 0;

                for (var i = 0; i < genome.Count; i++)
                {
                    total += genome[i].Fitness;
                }

                return total;
            }
        }

        public virtual double AverageFitness => TotalFitness / ((double)genome.Count);

        public virtual bool HasPerfectSolution
        {
            get => hasPerfectSolution;
            set => hasPerfectSolution = value;
        }

        public virtual int PerfectSolutionIndex
        {
            get => perfectSolutionIndex;
            set => perfectSolutionIndex = value;
        }

        public virtual Chromosome<T> this[int index]
        {
            get => genome[index];
            set => genome[index] = value;
        }

        public object Clone()
        {
            Population<T> newPopulation;

            newPopulation = (Population<T>)MemberwiseClone();
            for (var i = 0; i < genome.Count; i++)
            {
                newPopulation.Genome.Add((Chromosome<T>)genome[i].Clone());
            }

            return newPopulation;
        }

        public virtual Population<T> FastEmptyInstance()
        {
            Population<T> clone;

            //Clone the actual chromosome
            clone = (Population<T>)MemberwiseClone();

            //Restart the internal fields of the chromosome
            clone.Genome = new List<Chromosome<T>>();
            clone.HasPerfectSolution = false;
            clone.PerfectSolutionIndex = -1;

            return clone;
        }

        private bool ValidatePopulation(Population<T> population, ref string message)
        {
            if (population == null)
            {
                message = "The population can´t be null";
                return false;
            }

            if (ValidateGenome(population.Genome, ref message) == false)
            {
                return false;
            }

            return true;
        }

        public static bool ValidateGenome(List<Chromosome<T>> genome, ref string message)
        {
            if (genome == null)
            {
                message = "The genome can´t be null.";
                return false;
            }

            return true;
        }

    }
}
