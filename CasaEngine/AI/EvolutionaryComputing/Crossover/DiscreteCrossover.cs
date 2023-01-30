namespace CasaEngine.AI.EvolutionaryComputing.Crossover
{
    public sealed class DiscreteCrossover<T> : CrossoverAlgorithm<T>
    {

        public DiscreteCrossover(double probability, Random generator)
            : base(probability, generator) { }



        public override List<Chromosome<T>> Crossover(List<Chromosome<T>> parents)
        {
            List<Chromosome<T>> list;
            Chromosome<T> chromosome;

            if (parents.Count < 2)
                throw new Exception("The number of parents must be at least 2.");

            list = new List<Chromosome<T>>();

            //Test to see if there´s a crossover or not
            if (base.Crossover(parents) != null)
            {
                list.Add((Chromosome<T>)parents[generator.Next(0, parents.Count - 1)].Clone());

                return list;
            }

            chromosome = parents[0].FastEmptyInstance();

            //Get each gene from the genotype at random from the parents
            for (int i = 0; i < parents[0].Genotype.Count; i++)
                chromosome.Genotype.Add(parents[generator.Next(0, parents.Count - 1)][i]);

            list.Add(chromosome);

            return list;
        }

    }
}
