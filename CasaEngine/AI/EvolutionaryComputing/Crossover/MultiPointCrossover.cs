namespace CasaEngine.AI.EvolutionaryComputing.Crossover
{
    public sealed class MultiPointCrossover<T> : CrossoverAlgorithm<T>
    {

        internal int CrossoverPoints;



        public MultiPointCrossover(double probability, Random generator, int crossoverPoints)
            : base(probability, generator)
        {
            string message = string.Empty;

            if (ValidateCrossoverPoints(crossoverPoints, ref message) == false)
            {
                throw new AiException("probability", GetType().ToString(), message);
            }

            CrossoverPoints = crossoverPoints;
        }



        public override List<Chromosome<T>> Crossover(List<Chromosome<T>> parents)
        {
            List<Chromosome<T>> list;
            List<int> pointsList;
            int point;

            //This algorithm uses only 2 parents
            if (parents.Count != 2)
            {
                throw new Exception("The number of parents must be 2.");
            }

            if (CrossoverPoints > parents[0].Genotype.Count)
            {
                throw new Exception("The number of crossover points must be smaller than the genotype size of the parents.");
            }

            list = new List<Chromosome<T>>();

            //Test to see if there´s a crossover or not
            if (base.Crossover(parents) != null)
            {
                list.Add((Chromosome<T>)parents[0].Clone());
                list.Add((Chromosome<T>)parents[1].Clone());

                return list;
            }

            //Generate the crossover points
            pointsList = new List<int>();
            for (int i = 0; i < CrossoverPoints; i++)
            {
                point = Generator.Next(1, parents[0].Genotype.Count - 1);
                while (pointsList.Contains(point))
                    point = Generator.Next(1, parents[0].Genotype.Count - 1);

                pointsList.Add(point);
            }

            //Sort the points list in ascending order
            pointsList.Sort();

            CrossParents(list, parents, pointsList);
            return list;
        }

        private void CrossParents(List<Chromosome<T>> list, List<Chromosome<T>> parents, List<int> pointsList)
        {
            Chromosome<T> chromosome1, chromosome2;
            int i, j, start, end, selectedParent;

            chromosome1 = parents[0].FastEmptyInstance();
            chromosome2 = parents[0].FastEmptyInstance();

            start = 0;
            end = pointsList[0];
            selectedParent = 0;

            i = 0;
            while (true)
            {
                //Until we get to a crossover point, we copy all the genes of the selected parent
                for (j = start; j < end; j++)
                {
                    chromosome1.Genotype.Add(parents[selectedParent].Genotype[j]);
                    chromosome2.Genotype.Add(parents[(selectedParent + 1) % 2].Genotype[j]);
                }

                selectedParent = (selectedParent + 1) % 2;

                //Check if we have reached the end
                if (i + 1 == pointsList.Count)
                {
                    break;
                }

                //Set the next crossover point and change parent
                start = pointsList[i];
                end = pointsList[i + 1];
                i++;
            }

            //Copy the last genomes
            for (i = j; i < parents[0].Genotype.Count; i++)
            {
                chromosome1.Genotype.Add(parents[selectedParent].Genotype[i]);
                chromosome2.Genotype.Add(parents[(selectedParent + 1) % 2].Genotype[i]);
            }

            //Add the two new children to the list
            list.Add(chromosome1);
            list.Add(chromosome2);
        }



        public static bool ValidateCrossoverPoints(int crossoverPoints, ref string message)
        {
            if (crossoverPoints < 1)
            {
                message = "The number of crossover points must be at least one.";
                return false;
            }

            return true;
        }

    }
}
