#region Using Directives

using System;
using System.Collections.Generic;

using CasaEngine.AI.EvolutionaryComputing.Crossover;
using CasaEngine.AI.EvolutionaryComputing.Mutation;
using CasaEngine.AI.EvolutionaryComputing.Replacement;
using CasaEngine.AI.EvolutionaryComputing.Selection;



#endregion

namespace CasaEngine.AI.EvolutionaryComputing
{
	/// <summary>
	/// This class represents and evolutive algorithm (a genetic algorithm or an evolutive strategy)
	/// </summary>
	/// <typeparam name="T">The type of the genes</typeparam>
	public class EvolutionaryAlgorithm<T>
	{
		#region Events

		/// <summary>
		/// Event fired everytime the algorithm makes one loop
		/// </summary>
		//public event NewStepEventHandler<T> NewStep;

		#endregion

		#region Fields

		/// <summary>
		/// The mutation algorithm
		/// </summary>
		protected internal MutationMethod<T> mutation;

		/// <summary>
		/// The replacement method
		/// </summary>
		protected internal ReplacementMethod<T> replacement;

		/// <summary>
		/// The selection method
		/// </summary>
		protected internal SelectionMethod<T> selection;

		/// <summary>
		/// The problem to solve
		/// </summary>
		protected internal IEvolutionaryProblem<T> problem;

		/// <summary>
		/// The number of offsprings to create every generation
		/// </summary>
		protected internal int numberOffsprings;

		/// <summary>
		/// Number of generations to execute the algorithm
		/// </summary>
		protected internal int numberGenerations;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="problem">Problem to try to solve</param>
		/// <param name="selection">Selection operator</param>
		/// <param name="mutation">Mutation operator</param>
		/// <param name="replacement">Replacement operator</param>
		/// <param name="numberOffsprings">Number of offsprings that will be created every generation</param>
		/// <param name="numberGenerations">Number of generations to execute the algorithm</param>
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

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the mutation operator
		/// </summary>
		public MutationMethod<T> Mutation
		{
			get { return mutation; }
			set
			{
				String message = String.Empty;

				if (ValidateMutation(value, ref message) == false)
					throw new AIException("mutation", this.GetType().ToString(), message);

				mutation = value;
			}
		}

		/// <summary>
		/// Gets or sets the replacement operator
		/// </summary>
		public ReplacementMethod<T> Replacement
		{
			get { return replacement; }
			set
			{
				String message = String.Empty;

				if (ValidateReplacement(value, ref message) == false)
					throw new AIException("replacement", this.GetType().ToString(), message);

				replacement = value;
			}
		}

		/// <summary>
		/// Gets or sets the selection operator
		/// </summary>
		public SelectionMethod<T> Selection
		{
			get { return selection; }
			set
			{
				String message = String.Empty;

				if (ValidateSelection(value, ref message) == false)
					throw new AIException("selection", this.GetType().ToString(), message);

				selection = value;
			}
		}

		/// <summary>
		/// Gets or sets the evolutive problem
		/// </summary>
		public IEvolutionaryProblem<T> Problem
		{
			get { return problem; }
			set
			{
				String message = String.Empty;

				if (ValidateProblem(value, ref message) == false)
					throw new AIException("problem", this.GetType().ToString(), message);

				problem = value;
			}
		}

		/// <summary>
		/// Gets or sets the number of offsprings
		/// </summary>
		public int NumberOffsprings
		{
			get { return numberOffsprings; }
			set
			{
				String message = String.Empty;

				if (ValidateOffsprings(value, ref message) == false)
					throw new AIException("numberOffsprings", this.GetType().ToString(), message);

				numberOffsprings = value;
			}
		}

		/// <summary>
		/// Gets or sets the number of generations
		/// </summary>
		public int NumberGenerations
		{
			get { return numberGenerations; }
			set
			{
				String message = String.Empty;

				if (ValidateGenerations(value, ref message) == false)
					throw new AIException("numberGenerations", this.GetType().ToString(), message);

				numberGenerations = value;
			}
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the mutation value is correct (not null)
		/// </summary>
		/// <param name="mutation">The mutation value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateMutation(MutationMethod<T> mutation, ref string message)
		{
			if (mutation == null)
			{
				message = "The mutation operator can´t be null";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Validates if the replacement value is correct (not null)
		/// </summary>
		/// <param name="replacement">The replacement value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateReplacement(ReplacementMethod<T> replacement, ref string message)
		{
			if (replacement == null)
			{
				message = "The replacement operator can´t be null";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Validates if the selection value is correct (not null)
		/// </summary>
		/// <param name="selection">The selection value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateSelection(SelectionMethod<T> selection, ref string message)
		{
			if (selection == null)
			{
				message = "The selection operator can´t be null";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Validates if the problem value is correct (not null)
		/// </summary>
		/// <param name="problem">The problem value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateProblem(IEvolutionaryProblem<T> problem, ref string message)
		{
			if (problem == null)
			{
				message = "The problem to solve can´t be null";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Validates if the number of offsprings value is correct (>= 1)
		/// </summary>
		/// <param name="numberOffsprings">The number of offsprings value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateOffsprings(int numberOffsprings, ref string message)
		{
			if (numberOffsprings < 1)
			{
				message = "At least one offspring must be generated";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Validates if the number of generations value is correct (>= 1)
		/// </summary>
		/// <param name="numberGenerations">The number of generations value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateGenerations(int numberGenerations, ref string message)
		{
			if (numberGenerations < 1)
			{
				message = "The number of generations must be at least 1";
				return false;
			}

			return true;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Method that solves the problem in a number of generations without improvement.
		/// The algorithm finishes when no better solution is found in numberGenerations
		/// generations
		/// </summary>
		/// <returns>The best individual found</returns>
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

		/// <summary>
		/// Method that solves the problem in a number of generations
		/// </summary>
		/// <returns>The best individual found</returns>
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

		#endregion
	}
}
