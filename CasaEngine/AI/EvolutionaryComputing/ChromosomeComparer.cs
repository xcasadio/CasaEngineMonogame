namespace CasaEngine.AI.EvolutionaryComputing
{
    public class ChromosomeComparer<T> : IComparer<Chromosome<T>>
    {

        protected internal EvolutionObjective order;



        public ChromosomeComparer(EvolutionObjective order)
        {
            this.order = order;
        }



        public int Compare(Chromosome<T> x, Chromosome<T> y)
        {
            if (order == EvolutionObjective.Minimize)
                return x.Fitness.CompareTo(y.Fitness);

            else
                return -(x.Fitness.CompareTo(y.Fitness));
        }

    }
}
