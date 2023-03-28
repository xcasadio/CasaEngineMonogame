namespace CasaEngine.Framework.AI.EvolutionaryComputing;

public class ChromosomeComparer<T> : IComparer<Chromosome<T>>
{

    protected internal EvolutionObjective Order;



    public ChromosomeComparer(EvolutionObjective order)
    {
        Order = order;
    }



    public int Compare(Chromosome<T> x, Chromosome<T> y)
    {
        if (Order == EvolutionObjective.Minimize)
        {
            return x.Fitness.CompareTo(y.Fitness);
        }

        else
        {
            return -x.Fitness.CompareTo(y.Fitness);
        }
    }

}