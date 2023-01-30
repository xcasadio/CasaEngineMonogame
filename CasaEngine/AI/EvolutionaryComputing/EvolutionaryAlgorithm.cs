using CasaEngine.AI.EvolutionaryComputing.Mutation;
using CasaEngine.AI.EvolutionaryComputing.Replacement;
using CasaEngine.AI.EvolutionaryComputing.Selection;




namespace CasaEngine.AI.EvolutionaryComputing
{
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
            String message = String.Empty;

            if (ValidateMutation(mutation, ref message) == false)
                throw new AIException("mutation", this.GetType().ToString(), message);

            if (ValidateReplacement(replacement, ref message) == false)
                throw new AIException("replacement", this.GetType().ToString(), message);

            if (ValidateSelection(selection, ref message) == false)
                throw new AIException("selection", this.GetType().ToString(), message);

            if (ValidateProblem(problem, ref message) == false)
                throw new AIException("problem", this.GetType().ToString(), message);

            if (ValidateOffsprings(numberOffsprings, ref message) == false)
                throw new AIException("numberOffsprings", this.GetType().ToString(), message);

            if (ValidateGenerations(numberGenerations, ref message) == false)
                throw new AIException("numberGenerations", this.GetType().ToString(), message);

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
                String message = String.Empty;

                if (ValidateMutation(value, ref message) == false)
                    throw new AIException("mutation", this.GetType().ToString(), message);

                mutation = value;
            }
        }

        public ReplacementMethod<T> Replacement
        {
            get => replacement;
            set
            {
                String message = String.Empty;

                if (ValidateReplacement(value, ref message) == false)
                    throw new AIException("replacement", this.GetType().ToString(), message);

                replacement = value;
            }
        }

        public SelectionMethod<T> Selection
        {
            get => selection;
            set
            {
                String message = String.Empty;

                if (ValidateSelection(value, ref message) == false)
                    throw new AIException("selection", this.GetType().ToString(), message);

                selection = value;
            }
        }

        public IEvolutionaryProblem<T> Problem
        {
            get => problem;
            set
            {
                String message = String.Empty;

                if (ValidateProblem(value, ref message) == false)
                    throw new AIException("problem", this.GetType().ToString(), message);

                problem = value;
            }
        }

        public int NumberOffsprings
        {
            get => numberOffsprings;
            set
            {
                String message = String.Empty;

                if (ValidateOffsprings(value, ref message) == false)
                    throw new AIException("numberOffsprings", this.GetType().ToString(), message);

                numberOffsprings = value;
            }
        }

        public int NumberGenerations
        {
            get => numberGenerations;
            set
            {
                String message = String.Empty;

                if (ValidateGenerations(value, ref message) == false)
                    throw new AIException("numberGenerations", this.GetType().ToString(), message);

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

                if (parents.HasPerfectSolution == true)
                    return parents[parents.PerfectSolutionIndex];

                //Create the offsprings population
                offsprings = selection(parents, numberOffsprings);

                //Mutate the offsprings
                mutation(offsprings);

                //Calculate their fitness and test if a perfect solution was found
                problem.CalculateFitness(offsprings);

                if (offsprings.HasPerfectSolution == true)
                    return offsprings[offsprings.PerfectSolutionIndex];

                //Choose the survivors
                survivors = replacement(parents, offsprings);

                //Calculate the survivors fitness
                problem.CalculateFitness(survivors);

                //Sort parents and survivors to compare them
                parents.Genome.Sort(comparer);
                survivors.Genome.Sort(comparer);

                //If the parents where better than the survivors, there wasn´t any improvement in this generation
                if (comparer.Compare(parents[0], survivors[0]) != -1)
                    evaluationsWithoutImprovement++;

                else
                    evaluationsWithoutImprovement = 0;

                parents = survivors;

                if (evaluationsWithoutImprovement >= numberGenerations)
                    break;
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

            for (int i = 0; i < numberGenerations; i++)
            {
                //Calculate the fitness of the parents and test if a perfect solution was found
                problem.CalculateFitness(parents);

                if (parents.HasPerfectSolution == true)
                    return parents[parents.PerfectSolutionIndex];

                //Select the offsprings population
                offsprings = selection(parents, numberOffsprings);

                //Mutate the offsprings
                mutation(offsprings);

                //Calculate their fitness and test if a perfect solution was found
                problem.CalculateFitness(offsprings);

                if (offsprings.HasPerfectSolution == true)
                    return offsprings[offsprings.PerfectSolutionIndex];

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
}
