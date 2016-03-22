using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.LanguageModel.Provision.EditCommands;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// Holds a <see cref="CommandSequenceClass"/> 
	/// as feature to take part in a feature function computation of a word.
	/// </summary>
	[Serializable]
	public class WordFeature
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="id">The ID of the feature.</param>
		/// <param name="clazz">The command sequence class which defines this feature.</param>
		public WordFeature(int id, CommandSequenceClass clazz)
		{
			if (id < 0) throw new ArgumentException("id must not be negative.", "id");
			if (clazz == null) throw new ArgumentNullException("clazz");

			this.ID = id;
			this.Class = clazz;
		}

		/// <summary>
		/// The ID of the feature.
		/// </summary>
		public int ID { get; internal set; }

		/// <summary>
		/// The command sequence class which defines this feature.
		/// </summary>
		public CommandSequenceClass Class { get; private set; }
	}
}
