namespace CasaEngine.AI.EvolutionaryComputing.Crossover
{
    public sealed class PartiallyMappedCrossover : CrossoverAlgorithm<int>
    {

        public PartiallyMappedCrossover(double probability, Random generator)
            : base(probability, generator) { }



        public override List<Chromosome<int>> Crossover(List<Chromosome<int>> parents)
        {
            List<Chromosome<int>> list;
            Chromosome<int> chromosome;
            int first, second, temp;

            if (parents.Count != 2)
                throw new Exception("The number of parents must be 2.");

            list = new List<Chromosome<int>>();

            //Test to see if there´s a crossover or not
            if (base.Crossover(parents) != null)
            {
                list.Add((Chromosome<int>)parents[Generator.Next(0, 1)].Clone());

                return list;
            }

            //Generate 2 crossover points to cross the parents
            first = Generator.Next(0, parents[0].Genotype.Count - 1);
            second = Generator.Next(0, parents[0].Genotype.Count - 1);

            while (second == first)
                second = Generator.Next(0, parents[0].Genotype.Count - 1);

            // Order the indexes
            if (first > second)
            {
                temp = first;
                first = second;
                second = temp;
            }

            chromosome = Pmx(parents[0], parents[1], first, second);

            list.Add(chromosome);

            return list;
        }

        private Chromosome<int> Pmx(Chromosome<int> firstParent, Chromosome<int> secondParent, int first, int second)
        {
            List<int> selectedGenes, combinableGenes;
            Chromosome<int> chromosome;
            int[] genes;
            int position;

            //Create an empty array to copy the genes we select
            genes = new int[firstParent.Genotype.Count];
            for (int i = 0; i < genes.Length; i++)
                genes[i] = -1;

            //Copy all the genes of the first parent between the 2 crossover points
            for (int i = first; i < second; i++)
                genes[i] = firstParent[i];

            //Take note of the genes from the first and second parent that were between the crossover points
            selectedGenes = new List<int>();
            combinableGenes = new List<int>();
            for (int i = first; i < second; i++)
            {
                selectedGenes.Add(firstParent[i]);
                combinableGenes.Add(secondParent[i]);
            }

            //Check if some selected gene from the second parent was between the selected genes and we mark it
            for (int i = 0; i < selectedGenes.Count; i++)
                if (selectedGenes.Contains(combinableGenes[i]))
                    combinableGenes[i] = -1;

            //Delete from the selected genes of the second parent the marked genes
            while (combinableGenes.Contains(-1))
                combinableGenes.Remove(-1);

            //Search for the position to insert the non-marked genes
            for (int i = 0; i < combinableGenes.Count; i++)
            {
                position = SearchPosition(combinableGenes[i], firstParent, secondParent, first, second);
                genes[position] = combinableGenes[i];
            }

            //Copy the left genes
            for (int i = 0; i < genes.Length; i++)
                if (genes[i] == -1)
                    genes[i] = secondParent[i];

            //Create the new chromosome and return it
            chromosome = firstParent.FastEmptyInstance();

            for (int i = 0; i < genes.Length; i++)
                chromosome.Genotype.Add(genes[i]);

            return chromosome;
        }

        private int SearchPosition(int gen, Chromosome<int> firstParent, Chromosome<int> secondParent, int first, int second)
        {
            int origin, destiny, final;

            //Calculate search positions
            origin = secondParent.Genotype.IndexOf(gen);
            destiny = secondParent.Genotype.IndexOf(firstParent[origin]);

            //If destiny position is between crossover points, call this function recursively with the gene in that position
            if (destiny >= first && destiny <= second)
                final = SearchPosition(secondParent[destiny], firstParent, secondParent, first, second);

            else
                final = destiny;

            return final;
        }

    }
}
