using CasaEngine.Framework.AI.EvolutionaryComputing.Mutation;
using CasaEngine.Framework.AI.EvolutionaryComputing.Replacement;
using CasaEngine.Framework.AI.EvolutionaryComputing.Selection;

namespace CasaEngine.Framework.AI.EvolutionaryComputing;

public class EvolutionaryAlgorithm<T>
{
    //public event NewStepEventHandler<T> NewStep;

    protected internal MutationMethod<T> mutation;

    protected internal ReplacementMethod<T> replacement;

    protected internal SelectionMethod<T> selection;

    protected internal IEvolutionaryProblem<T> problem;

    protected internal int numberOffsprings;

    protected internal int numberGenerations;

    public EvolutionaryAlgorithm(IEvolutionaryProblem<T> problem, SelectionMethod<T> selection, MutationMethod<T> mutation, ReplacementMethod<T> replacement,
        int numberOffsprings, int numberGenerations)
    {
        var message = string.Empty;

        if (ValidateMutation(mutation, ref message) == false)
        {
            throw new AiException("mutation", GetType().ToString(), message);
        }

        if (ValidateReplacement(replacement, ref message) == false)
        {
            throw new AiException("replacement", GetType().ToString(), message);
        }

        if (ValidateSelection(selection, ref message) == false)
        {
            throw new AiException("selection", GetType().ToString(), message);
        }

        if (ValidateProblem(problem, ref message) == false)
        {
            throw new AiException("problem", GetType().ToString(), message);
        }

        if (ValidateOffsprings(numberOffsprings, ref message) == false)
        {
            throw new AiException("numberOffsprings", GetType().ToString(), message);
        }

        if (ValidateGenerations(numberGenerations, ref message) == false)
        {
            throw new AiException("numberGenerations", GetType().ToString(), message);
        }

        this.mutation = mutation;
        this.replacement = replacement;
        this.selection = selection;
        this.problem = problem;
        this.numberOffsprings = numberOffsprings;
        this.numberGenerations = numberGenerations;
    }

    public MutationMethod<T> Mutation
    {
        get => mutation;
        set
        {
            var message = string.Empty;

            if (ValidateMutation(value, ref message) == false)
            {
                throw new AiException("mutation", GetType().ToString(), message);
            }

            mutation = value;
        }
    }

    public ReplacementMethod<T> Replacement
    {
        get => replacement;
        set
        {
            var message = string.Empty;

            if (ValidateReplacement(value, ref message) == false)
            {
                throw new AiException("replacement", GetType().ToString(), message);
            }

            replacement = value;
        }
    }

    public SelectionMethod<T> Selection
    {
        get => selection;
        set
        {
            var message = string.Empty;

            if (ValidateSelection(value, ref message) == false)
            {
                throw new AiException("selection", GetType().ToString(), message);
            }

            selection = value;
        }
    }

    public IEvolutionaryProblem<T> Problem
    {
        get => problem;
        set
        {
            var message = string.Empty;

            if (ValidateProblem(value, ref message) == false)
            {
                throw new AiException("problem", GetType().ToString(), message);
            }

            problem = value;
        }
    }

    public int NumberOffsprings
    {
        get => numberOffsprings;
        set
        {
            var message = string.Empty;

            if (ValidateOffsprings(value, ref message) == false)
            {
                throw new AiException("numberOffsprings", GetType().ToString(), message);
            }

            numberOffsprings = value;
        }
    }

    public int NumberGenerations
    {
        get => numberGenerations;
        set
        {
            var message = string.Empty;

            if (ValidateGenerations(value, ref message) == false)
            {
                throw new AiException("numberGenerations", GetType().ToString(), message);
            }

            numberGenerations = value;
        }
    }

    public static bool ValidateMutation(MutationMethod<T> mutation, ref string message)
    {
        if (mutation == null)
        {
            message = "The mutation operator can´t be null";
            return false;
        }

        return true;
    }

    public static bool ValidateReplacement(ReplacementMethod<T> replacement, ref string message)
    {
        if (replacement == null)
        {
            message = "The replacement operator can´t be null";
            return false;
        }

        return true;
    }

    public static bool ValidateSelection(SelectionMethod<T> selection, ref string message)
    {
        if (selection == null)
        {
            message = "The selection operator can´t be null";
            return false;
        }

        return true;
    }

    public static bool ValidateProblem(IEvolutionaryProblem<T> problem, ref string message)
    {
        if (problem == null)
        {
            message = "The problem to solve can´t be null";
            return false;
        }

        return true;
    }

    public static bool ValidateOffsprings(int numberOffsprings, ref string message)
    {
        if (numberOffsprings < 1)
        {
            message = "At least one offspring must be generated";
            return false;
        }

        return true;
    }

    public static bool ValidateGenerations(int numberGenerations, ref string message)
    {
        if (numberGenerations < 1)
        {
            message = "The number of generations must be at least 1";
            return false;
        }

        return true;
    }

    public Chromosome<T> SolveWithImprovement()
    {
        int evaluationsWithoutImprovement;
        Population<T> parents, offsprings, survivors;
        List<Chromosome<T>> selected;
        ChromosomeComparer<T> comparer;

        evaluationsWithoutImprovement = 0;
        selected = new List<Chromosome<T>>();
        parents = problem.GenerateInitialPopulation();
        comparer = new ChromosomeComparer<T>(problem.Objective);

        while (true)
        {
            //Calculate the fitness of the parents and test if a perfect solution was found
            problem.CalculateFitness(parents);

            if (parents.HasPerfectSolution)
            {
                return parents[parents.PerfectSolutionIndex];
            }

            //Create the offsprings population
            offsprings = selection(parents, numberOffsprings);

            //Mutate the offsprings
            mutation(offsprings);

            //Calculate their fitness and test if a perfect solution was found
            problem.CalculateFitness(offsprings);

            if (offsprings.HasPerfectSolution)
            {
                return offsprings[offsprings.PerfectSolutionIndex];
            }

            //Choose the survivors
            survivors = replacement(parents, offsprings);

            //Calculate the survivors fitness
            problem.CalculateFitness(survivors);

            //Sort parents and survivors to compare them
            parents.Genome.Sort(comparer);
            survivors.Genome.Sort(comparer);

            //If the parents where better than the survivors, there wasn´t any improvement in this generation
            if (comparer.Compare(parents[0], survivors[0]) != -1)
            {
                evaluationsWithoutImprovement++;
            }

            else
            {
                evaluationsWithoutImprovement = 0;
            }

            parents = survivors;

            if (evaluationsWithoutImprovement >= numberGenerations)
            {
                break;
            }
        }

        //Return the best individual
        return parents[0];
    }

    public Chromosome<T> SolveWithoutImprovement()
    {
        Population<T> parents, offsprings, survivors;
        List<Chromosome<T>> selected;

        selected = new List<Chromosome<T>>();
        parents = problem.GenerateInitialPopulation();

        for (var i = 0; i < numberGenerations; i++)
        {
            //Calculate the fitness of the parents and test if a perfect solution was found
            problem.CalculateFitness(parents);

            if (parents.HasPerfectSolution)
            {
                return parents[parents.PerfectSolutionIndex];
            }

            //Select the offsprings population
            offsprings = selection(parents, numberOffsprings);

            //Mutate the offsprings
            mutation(offsprings);

            //Calculate their fitness and test if a perfect solution was found
            problem.CalculateFitness(offsprings);

            if (offsprings.HasPerfectSolution)
            {
                return offsprings[offsprings.PerfectSolutionIndex];
            }

            //Choose the survivors and replace the parents
            survivors = replacement(parents, offsprings);

            parents = survivors;
        }

        //Sort the final individuals
        parents.Genome.Sort(new ChromosomeComparer<T>(problem.Objective));

        //Return the best indivual
        return parents[0];
    }

}