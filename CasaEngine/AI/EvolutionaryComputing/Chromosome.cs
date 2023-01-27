#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;




#endregion

namespace CasaEngine.AI.EvolutionaryComputing
{
	/// <summary>
	/// This class represents a chromosome to use in a genetic algorithm or evolutionary strategy. A chromosome is
	/// composed by a list of genes and has an associated fitness value that indicates his "quality" as a solution
	/// for the problem we are trying to solve
	/// </summary>
	/// <typeparam name="T">The genes type. Can be anything</typeparam>
	[Serializable]
	public class Chromosome<T> : ICloneable
	{
		#region Fields

		/// <summary>
		/// Fitness of the chromosome
		/// </summary>
		protected internal double fitness;

		/// <summary>
		/// Genotype of the chromosome
		/// </summary>
		protected internal List<T> genotype;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <remarks>Creates an empty list of genes</remarks>
		public Chromosome()
		{
			genotype = new List<T>();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the fitness value of the chromosome
		/// </summary>
		public virtual double Fitness
		{
			get { return fitness; }
			set { fitness = value; }
		}

		/// <summary>
		/// Gets or sets the genotype of the chromosome
		/// </summary>
		public virtual List<T> Genotype
		{
			get { return genotype; }
			set
			{
				String message = String.Empty;

				if (ValidateGenotype(value, ref message) == false)
					throw new AIException("genotype", this.GetType().ToString(), message);

				genotype = value;
			}
		}

		/// <summary>
		/// Indexer to get or set a gene from the chromosome
		/// </summary>
		/// <param name="index">Index we want to access</param>
		/// <returns>The gene in the index position</returns>
		public virtual T this[int index]
		{
			get { return genotype[index]; }
			set { genotype[index] = value; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Creates a clone from this object
		/// </summary>
		/// <returns>The clone of the object</returns>
		public object Clone()
		{
			Chromosome<T> newChrom;
			MemoryStream memory;
			BinaryFormatter formater;
			object clone;

		//Best scenario, T is a ValueType
		if (typeof(T).IsValueType)
		{
			newChrom = (Chromosome<T>) MemberwiseClone();
			newChrom.Genotype.Clear();

			for (int i = 0; i < genotype.Count; i++)
				newChrom.Genotype.Add(genotype[i]);

			return newChrom;
		}

			//So so scenario, T implements ICloneable, lots of casts needed
			if (typeof(T).GetInterface("System.ICloneable", true) != null)
			{
				newChrom = (Chromosome<T>) MemberwiseClone();
				newChrom.Genotype.Clear();

				for (int i = 0; i < genotype.Count; i++)
					newChrom.Genotype.Add((T) ((ICloneable) genotype[i]).Clone());

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

		/// <summary>
		/// Returns a new instance of a chromosome
		/// </summary>
		/// <remarks>
		/// This way of getting an instance is faster than using reflection
		/// and the Activator method. This instance is the same as if
		/// calling the empty constructor to create a Chromosome
		/// </remarks>
		/// <returns>A new chromosome instance</returns>
		public virtual Chromosome<T> FastEmptyInstance()
		{
			Chromosome<T> clone;

			//Clone the actual chromosome
			clone = (Chromosome<T>) base.MemberwiseClone();

			//Restart the internal fields of the chromosome
			clone.Fitness = 0;
			clone.Genotype = new List<T>();

			return clone;
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the chromosome value is correct (not null)
		/// </summary>
		/// <param name="chromosome">The chromosome value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
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

		/// <summary>
		/// Validates if the genotype value is correct (not null)
		/// </summary>
		/// <param name="genotype">The genotype value to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the values are correct. False if they are not</returns>
		public static bool ValidateGenotype(List<T> genotype, ref String message)
		{
			if (genotype == null)
			{
				message = "The genotype list can´t be null.";
				return false;
			}

			return true;
		}

		#endregion
	}
}
